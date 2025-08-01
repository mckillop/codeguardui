using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using Microsoft.Azure.Cosmos;
using System.IO;
using System.Net.Mime;

namespace Atturra.CodeGuard;

public class Report
{
    private readonly ILogger<Report> _logger;

    public Report(ILogger<Report> logger)
    {
        _logger = logger;
    }

    [Function("GetReportsByClientId")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "report")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        string clientId = req.Query["clientId"]!;
        Container container = SharedLogic.GetCosmosDbContainer(SharedLogic.GetReportExecutionContainerId());
        QueryDefinition query = new QueryDefinition("SELECT * FROM c");
        FeedIterator<ReportExecution> iterator = container.GetItemQueryIterator<ReportExecution>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(clientId)
            }
        );
        List<ReportExecution> results = new();
        while (iterator.HasMoreResults)
        {
            var response = iterator.ReadNextAsync().GetAwaiter().GetResult();
            results.AddRange(response.Resource);
        }
        return new OkObjectResult(results);
    }
}