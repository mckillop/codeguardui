using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        try {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string apiKey = SharedLogic.GetRequiredQueryParam(req,"apiKey");
            string clientId = SharedLogic.GetRequiredQueryParam(req,"clientId");
            string clientSecretEncrypted = SharedLogic.GetRequiredQueryParam(req,"clientSecretEncrypted");
            string atomsphereAccountId = SharedLogic.GetRequiredQueryParam(req,"atomsphereAccountId");
            string atomsphereUsername = SharedLogic.GetRequiredQueryParam(req,"atomsphereUsername");
            string atomspherePasswordEncrypted = SharedLogic.GetRequiredQueryParam(req,"atomspherePasswordEncrypted");
            string? requestBody = await req.ReadAsStringAsync();
            JsonDocument bodyDoc = JsonDocument.Parse(requestBody!);

            // create an object with the configuration attributes
            var config = new
            {
                apiKey = apiKey,
                clientId = clientId,
                clientSecretEncrypted = clientSecretEncrypted,
                atomsphereAccountId = atomsphereAccountId,
                atomsphereUsername = atomsphereUsername,
                atomspherePasswordEncrypted = atomspherePasswordEncrypted
            };

            ServiceBusClient serviceBusClient = new ServiceBusClient(SharedLogic.GetServiceBusFQNS(), SharedLogic.GetAzureCredential());
            ServiceBusSender serviceBusSender = serviceBusClient.CreateSender(SharedLogic.GetServiceBusQueue());
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
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (AppException ex)
        {
            return await SharedLogic.AppExceptionAsync(req, ex);
        }
        catch (Exception ex)
        {
            return await SharedLogic.ExceptionAsync(req, ex);
        }
    }
}