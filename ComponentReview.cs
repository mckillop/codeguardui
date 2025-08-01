using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Messaging.ServiceBus;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Atturra.CodeGuard;

public class ComponentReview
{
    private readonly ILogger<ComponentReview> _logger;

    public ComponentReview(ILogger<ComponentReview> logger)
    {
        _logger = logger;
    }

    [Function("ComponentReview")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        string apiKey = req.Query["apiKey"]!;
        string clientId = req.Query["clientId"]!;
        string clientSecretEnrypted = req.Query["clientSecretEncrypted"]!;
        string atomsphereAccountId = req.Query["atomsphereAccountId"]!;
        string atomsphereUsername = req.Query["atomsphereUsername"]!;
        string atomspherePasswordEncrypted = req.Query["atomspherePasswordEncrypted"]!;

        // add query parameters to incoming request body
        //JsonDocument json = SharedLogic.RequestBodyToJsonDoc(req);
        var config = new
        {
            apiKey = apiKey,
            clientId = clientId,
            clientSecretEnrypted = clientSecretEnrypted,
            atomsphereAccountId = atomsphereAccountId,
            atomsphereUsername = atomsphereUsername,
            atomspherePasswordEncrypted = atomspherePasswordEncrypted
        };

        string fullyQualifiedNamespace = Environment.GetEnvironmentVariable("azureServiceBusFQNS")!;
        string queueName = Environment.GetEnvironmentVariable("azureServiceBusQueue")!;
        ServiceBusClient serviceBusClient = new ServiceBusClient(fullyQualifiedNamespace, SharedLogic.GetAzureCredential());
        ServiceBusSender serviceBusSender = serviceBusClient.CreateSender(queueName);
        /*
                                        string clientSecret = SharedLogic.DecryptPgpMessage(clientSecretEnrypted);
                                        string atomsphereBasicAuthToken = SharedLogic.AtomsphereBasicAuthToken(atomsphereUsername, atomspherePasswordEncrypted);
                                        string token = SharedLogic.CodeGuardToken(clientId, clientSecret);
                                        string mimeType = "application/pdf";
        */
        foreach (var r in SharedLogic.RequestBodyToJsonDoc(req).RootElement.EnumerateArray())
        {
            JsonNode msg = JsonNode.Parse(r.GetRawText())!;
            msg["configuration"] = JsonNode.Parse(JsonSerializer.Serialize(config));
            ServiceBusMessage message = new(msg.ToJsonString());
            serviceBusSender.SendMessageAsync(message).GetAwaiter().GetResult();
        }
/*                                  // generate the report
                                                string componentId = r.GetProperty("componentId").GetString()!;
                                                string url = SharedLogic.GetCodeGuardHost() + "/codeGuard/v1/accounts/" + atomsphereAccountId + "/components/" + componentId + "/review?timezone=Australia/Melbourne";
                                                HttpClient client = new();
                                                var content = new StringContent(r.GetProperty("reviewParameters").GetRawText(), Encoding.UTF8, "application/json");
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
                                                // create item in CosmosDB
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
                                                ItemResponse<ReportExecution> cosmosDbResponse = container.CreateItemAsync<ReportExecution>(
                                                    reportExecution,
                                                    new PartitionKey(reportExecution.clientId)
                                                ).GetAwaiter().GetResult();
                                            }
                                            req.HttpContext.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                                            //return new FileContentResult(cgResponse.Content.ReadAsByteArrayAsync().Result, MediaTypeNames.Application.Pdf);
                                            */
            return new OkResult();
    }
}