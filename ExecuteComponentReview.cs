using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

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
        JsonDocument body = JsonDocument.Parse(message.Body.ToString());
        JsonElement config = body.RootElement.GetProperty("configuration");
        string apiKey = config.GetProperty("apiKey").GetString()!;
        _logger.LogError("apiKey=" + apiKey);
        //string clientId = config.GetProperty("clientId").GetString()!;
        //string clientSecretEnrypted = config.GetProperty("clientSecretEncrypted").GetString()!;
        //string atomsphereAccountId = config.GetProperty("atomsphereAccountId").GetString()!;
        //string atomsphereUsername = config.GetProperty("atomsphereUsername").GetString()!;
        //string atomspherePasswordEncrypted = config.GetProperty("atomspherePasswordEncrypted").GetString()!;
        //JsonElement review = body.RootElement.GetProperty("componentReview");
        //string componentId = review.GetProperty("componentId").GetString()!;

        //string clientSecret = SharedLogic.DecryptPgpMessage(clientSecretEnrypted);
        //string atomsphereBasicAuthToken = SharedLogic.AtomsphereBasicAuthToken(atomsphereUsername, atomspherePasswordEncrypted);
        //string token = SharedLogic.CodeGuardToken(clientId, clientSecret);
        //string mimeType = "application/pdf";

        //int daysToLive = 7;
        // create an item in CosmosDB
        //int ttl = daysToLive * 24 * 60 * 60; // in seconds
        //ReportExecution reportExecution = new()
        //{
        //    clientId = clientId,
        //    id = Guid.NewGuid(),
        //    timestamp = DateTime.UtcNow.ToString("o"),
        //    reportType = "Component Review",
        //    status = "Completed",
        //    componentId = componentId,
        //    mimeType = mimeType,
        //blobId = blobId,
        //reportUrl = blobUrl,
        //    ttl = ttl
        //};
        //Container container = SharedLogic.GetCosmosDbContainer(SharedLogic.GetReportExecutionContainerId());
        //ItemResponse<ReportExecution> cosmosDbResponse = await container.CreateItemAsync<ReportExecution>(
        //    reportExecution,
        //    new PartitionKey(reportExecution.clientId)
        //);

        // Complete the message
        await messageActions.CompleteMessageAsync(message);
    }
}