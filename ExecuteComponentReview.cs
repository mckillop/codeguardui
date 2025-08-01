using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker.Extensions.ServiceBus;

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

        // Complete the message
        await messageActions.CompleteMessageAsync(message);
    }
}