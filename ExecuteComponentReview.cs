using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Cosmos;
//using Microsoft.Azure.Functions.Worker.Http;
//using System.Net;

namespace Atturra.CodeGuard;

public class ExecuteComponentReview
{
    private readonly ILogger _logger;
    public ExecuteComponentReview(ILogger<ExecuteComponentReview> logger)
    {
        _logger = logger;
    }
    [Function("ExecuteComponentReview")]
    public async Task Run(
        [ServiceBusTrigger("%azureServiceBusQueue%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
        _logger.LogInformation("Message Body: {body}", message.Body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);
        await ProcessMessageAsync(message.Body.ToString());
        await messageActions.CompleteMessageAsync(message);
    }
    public async Task ProcessMessageAsync(string message)
    {
        await Task.Delay(1000);
        JsonDocument body = JsonDocument.Parse(message);
        JsonElement config = body.RootElement.GetProperty("configuration");
        string apiKey = config.GetProperty("apiKey").GetString()!;
        string clientId = config.GetProperty("clientId").GetString()!;
        string clientSecretEncrypted = config.GetProperty("clientSecretEncrypted").GetString()!;
        string atomsphereAccountId = config.GetProperty("atomsphereAccountId").GetString()!;
        string atomsphereUsername = config.GetProperty("atomsphereUsername").GetString()!;
        string atomspherePasswordEncrypted = config.GetProperty("atomspherePasswordEncrypted").GetString()!;
        JsonElement review = body.RootElement.GetProperty("componentReview");
        string componentId = review.GetProperty("componentId").GetString()!;

        string clientSecret = SharedLogic.DecryptPgpMessage(clientSecretEncrypted);
        string atomsphereBasicAuthToken = SharedLogic.AtomsphereBasicAuthToken(atomsphereUsername, atomspherePasswordEncrypted);
        string token = SharedLogic.CodeGuardToken(clientId, clientSecret);
        string mimeType = "application/pdf";

        // generate the report
        string url = SharedLogic.GetCodeGuardHost()
            + "/codeGuard/v1/accounts/"
            + atomsphereAccountId
            + "/components/"
            + componentId
            + "/review?timezone=Australia/Melbourne";
        HttpClient client = new();
        var content = new StringContent(review.GetProperty("reviewParameters").GetRawText(), Encoding.UTF8, "application/json");
        var cgRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        cgRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        cgRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(mimeType));
        cgRequest.Headers.Add("API-Key", apiKey);
        cgRequest.Headers.Add("AS-Authorisation", "Basic " + atomsphereBasicAuthToken);
        HttpResponseMessage cgResponse = client.Send(cgRequest);
        int daysToLive = 7;
        // store report in Azure Blob
        string blobId = cgResponse.Content.Headers.ContentDisposition!.FileName!.Trim('"');
        Stream dataStream = cgResponse.Content.ReadAsStreamAsync().Result;
        string blobUrl = SharedLogic.UploadBlob(blobId, contentType: mimeType, dataStream, daysToLive);
        // create an item in CosmosDB
        int ttl = daysToLive * 24 * 60 * 60; // in seconds
        ReportExecution reportExecution = new()
        {
            clientId = clientId,
            id = Guid.NewGuid(),
            timestamp = DateTime.UtcNow.ToString("o"),
            reportType = "Component Review",
            status = "Completed",
            componentId = componentId,
            mimeType = mimeType,
            blobId = blobId,
            reportUrl = blobUrl,
            ttl = ttl
        };
        Container container = SharedLogic.GetCosmosDbContainer(SharedLogic.GetReportExecutionContainerId());
        ItemResponse<ReportExecution> cosmosDbResponse = await container.CreateItemAsync<ReportExecution>(
            reportExecution,
            new PartitionKey(reportExecution.clientId)
        );
    }
/*    [Function("TestExecuteComponentReview")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "testExecuteComponentReview")] HttpRequestData req)
    {
        string? body = await req.ReadAsStringAsync();
        await ProcessMessageAsync(body!);
        return req.CreateResponse(HttpStatusCode.OK);
    }*/
}