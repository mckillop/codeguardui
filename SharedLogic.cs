using Azure.Identity;
using Azure.Core;
using System.Text;
using System.Text.Json;
using PgpCore;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Atturra.CodeGuard;

public static class SharedLogic
{
    internal static string GetReportExecutionContainerId()
    {
        return "ReportExecution";
    }
    internal static string DecryptPgpMessage(string enryptedMessage)
    {
        string pgpPrivateKeyBase64 = Environment.GetEnvironmentVariable("pgpPrivateKey")!;
        string pgpPrivateKey = Encoding.UTF8.GetString(Convert.FromBase64String(pgpPrivateKeyBase64));
        string pgpMessage = Encoding.UTF8.GetString(Convert.FromBase64String(enryptedMessage));
        EncryptionKeys encryptionKeys = new EncryptionKeys(pgpPrivateKey, "");
        PGP pgp = new PGP(encryptionKeys);
        return pgp.Decrypt(pgpMessage);
    }
    internal static string AtomsphereBasicAuthToken(string atomsphereUsername, string atomspherePasswordEncrypted)
    {
        string credentials = atomsphereUsername + ":" + DecryptPgpMessage(atomspherePasswordEncrypted);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
    }
    internal static TokenCredential GetAzureCredential()
    {
        if (Environment.GetEnvironmentVariable("isLocal") == "true")
        {
            return new AzureCliCredential();
        }
        else
        {
            return new DefaultAzureCredential();
        }
    }
    internal static string GetBlobContainerUri()
    {
        return Environment.GetEnvironmentVariable("blobContainerUri")!;
    }
    internal static string UploadBlob(string blobId, string contentType, Stream data, int daysToLive)
    {   // returns a URL to the blob with a SAS token
        string storageAccountName = Environment.GetEnvironmentVariable("storageAccountName")!;
        string blobContainerName = Environment.GetEnvironmentVariable("blobContainerName")!;
        BlobServiceClient blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), GetAzureCredential());
        BlobClient blobClient = blobServiceClient
            .GetBlobContainerClient(blobContainerName)
            .GetBlobClient(blobId);
        blobClient.Upload(data, new BlobHttpHeaders { ContentType = contentType });
        DateTimeOffset startsOn = DateTimeOffset.UtcNow;
        DateTimeOffset expiresOn = startsOn.AddDays(daysToLive);
        UserDelegationKey userDelegationKey = blobServiceClient
            .GetUserDelegationKeyAsync(startsOn: null, expiresOn: expiresOn)
            .GetAwaiter()
            .GetResult();
        BlobSasBuilder sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobContainerName,
            BlobName = blobId,
            Resource = "b",
            StartsOn = startsOn,
            ExpiresOn = expiresOn
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        string sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, storageAccountName).ToString();
        return $"{blobClient.Uri}?{sasToken}";
    }
    internal static CosmosClient GetCosmosClient()
    {
        string endpointUri = Environment.GetEnvironmentVariable("cosmosDbEndpointUri")!;
        return new CosmosClient(endpointUri, GetAzureCredential());
    }

    internal static Container GetCosmosDbContainer(string containerId)
    {
        string databaseId = Environment.GetEnvironmentVariable("cosmosDbDatabase")!;
        return GetCosmosClient().GetContainer(databaseId, containerId);
    }

    internal static string GenerateToken(Container container, string clientId, string clientSecret, string itemId)
    {
        // HTTP call to generate the token
        string url = Environment.GetEnvironmentVariable("codeguardAccessTokenUrl")!;
        string scope = Environment.GetEnvironmentVariable("codeguardAccessTokenScope")!;
        HttpClient client = new();
        var formData = new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("scope", scope),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        };
        var content = new FormUrlEncodedContent(formData);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        HttpResponseMessage httpResponse = client.Send(request);
        string responseBody = httpResponse.Content.ReadAsStringAsync().Result;
        var document = JsonDocument.Parse(responseBody);

        // store the token in CosmosDB
        int ttl = document.RootElement.GetProperty("expires_in").GetInt16() - 60; // leave one minute margin for token use
        CodeGuardAccessToken newToken = new()
        {
            clientId = clientId,
            id = itemId,
            accessToken = document.RootElement.GetProperty("access_token").GetString()!,
            ttl = ttl,
            timestamp = DateTime.UtcNow.ToString("o")
        };
        ItemResponse<CodeGuardAccessToken> cosmosDbResponse = container.UpsertItemAsync<CodeGuardAccessToken>(
            newToken,
            new PartitionKey(newToken.clientId)
        ).GetAwaiter().GetResult();
        return newToken.accessToken;
    }
    internal static string CodeGuardToken(string clientId, string clientSecret)
    {
        string containerId = "CodeGuardAccessToken";
        string itemId = "default"; // only need one item per clientId, and this is the partition key        

        Container container = GetCosmosDbContainer(containerId);
        try
        {
            CodeGuardAccessToken existingToken = container
                .ReadItemAsync<CodeGuardAccessToken>(itemId, new PartitionKey(clientId))
                .GetAwaiter()
                .GetResult();
            return existingToken.accessToken;
        }
        catch (Exception)
        { // no token in CosmosDB
            return GenerateToken(container, clientId, clientSecret, itemId);
        }
    }

    internal static string GetCodeGuardHost()
    {
        return Environment.GetEnvironmentVariable("codeguardHost")!;
    }

    public static JsonDocument RequestBodyToJsonDoc(HttpRequest request)
    {
        string requestBody;
        using (var reader = new StreamReader(request.Body))
        {
            requestBody = reader.ReadToEnd(); // Synchronous read
        }
        return JsonDocument.Parse(requestBody);
    }

    public static string GetServiceBusFQNS()
    {
        return Environment.GetEnvironmentVariable("ServiceBusConnection__fullyQualifiedNamespace")!;
    }
    public static string GetServiceBusQueue()
    {
        return Environment.GetEnvironmentVariable("azureServiceBusQueue")!;
    }
}