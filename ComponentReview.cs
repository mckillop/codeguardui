using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using Azure.Messaging.ServiceBus;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Atturra.CodeGuard;

public class ComponentReview
{
    private readonly ILogger<ComponentReview> _logger;

    public ComponentReview(ILogger<ComponentReview> logger)
    {
        _logger = logger;
    }

    [Function("ComponentReview")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        string apiKey = req.Query["apiKey"]!;
        string clientId = req.Query["clientId"]!;
        string clientSecretEnrypted = req.Query["clientSecretEncrypted"]!;
        string atomsphereAccountId = req.Query["atomsphereAccountId"]!;
        string atomsphereUsername = req.Query["atomsphereUsername"]!;
        string atomspherePasswordEncrypted = req.Query["atomspherePasswordEncrypted"]!;
        string? requestBody = await req.ReadAsStringAsync();
        JsonDocument bodyDoc = JsonDocument.Parse(requestBody!);

        // create an object with the configuration attributes
        var config = new
        {
            apiKey = apiKey,
            clientId = clientId,
            clientSecretEnrypted = clientSecretEnrypted,
            atomsphereAccountId = atomsphereAccountId,
            atomsphereUsername = atomsphereUsername,
            atomspherePasswordEncrypted = atomspherePasswordEncrypted
        };

        ServiceBusClient serviceBusClient = new ServiceBusClient(SharedLogic.GetServiceBusFQNS(), SharedLogic.GetAzureCredential());
        ServiceBusSender serviceBusSender = serviceBusClient.CreateSender(SharedLogic.GetServiceBusQueue());
        /*
                                        string clientSecret = SharedLogic.DecryptPgpMessage(clientSecretEnrypted);
                                        string atomsphereBasicAuthToken = SharedLogic.AtomsphereBasicAuthToken(atomsphereUsername, atomspherePasswordEncrypted);
                                        string token = SharedLogic.CodeGuardToken(clientId, clientSecret);
                                        string mimeType = "application/pdf";
        */
        foreach (var r in bodyDoc.RootElement.EnumerateArray())
        {
            // create a message with the configuration attribute and the details of the report request
            JsonNode msg = new JsonObject
            {
                ["componentReview"] = JsonNode.Parse(r.GetRawText())!, // contains the componentId etc from the array in the request body received
                ["configuration"] = JsonNode.Parse(JsonSerializer.Serialize(config)) // add configuration received as query parameters
            };
            // send the message to Azure Service Bus
            ServiceBusMessage message = new(msg.ToJsonString());
            await serviceBusSender.SendMessageAsync(message);
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
            return req.CreateResponse(HttpStatusCode.OK);
    }
}