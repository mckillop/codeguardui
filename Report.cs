using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using Microsoft.Azure.Cosmos;
using System.IO;
using System.Net.Mime;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Atturra.CodeGuard;

public class Report
{
    private readonly ILogger<Report> _logger;

    public Report(ILogger<Report> logger)
    {
        _logger = logger;
    }

    [Function("GetReportsByClientId")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "report")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string clientId = SharedLogic.GetRequiredQueryParameter(req, "clientId");
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
                var cosmosDbResponse = await iterator.ReadNextAsync();
                results.AddRange(cosmosDbResponse.Resource);
            }

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            await response.WriteAsJsonAsync(results);
            return response;
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