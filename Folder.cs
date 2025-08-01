using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Atturra.CodeGuard;

public class Folder
{
    private readonly ILogger<Folder> _logger;

    public Folder(ILogger<Folder> logger)
    {
        _logger = logger;
    }

    [Function("GetAllFolders")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "folder")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        string atomsphereAccountId = req.Query["atomsphereAccountId"]!;
        string atomsphereUsername = req.Query["atomsphereUsername"]!;
        string atomspherePasswordEncrypted = req.Query["atomspherePasswordEncrypted"]!;

        string basicAuthToken = SharedLogic.AtomsphereBasicAuthToken(atomsphereUsername, atomspherePasswordEncrypted);
        string baseUrl = "https://api.boomi.com/api/rest/v1/" + atomsphereAccountId + "/Folder";
        string url = baseUrl + "/query";
        var body = new
        {
            QueryFilter = new
            {
                expression = new Dictionary<string, object>
                {
                    { "property", "deleted" },
                    { "operator", "EQUALS" },
                    { "argument", new[] { false } }
                }
            }
        };
        string json = JsonSerializer.Serialize(body);
        HttpClient client = new();
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var atomsphereReq = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        List<string> folders = new List<string>();
        do
        {
            atomsphereReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthToken);
            atomsphereReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage atomsphereResponse = client.Send(atomsphereReq);
            string responseBody = await atomsphereResponse.Content.ReadAsStringAsync();
            var document = JsonDocument.Parse(responseBody);
            foreach (var f in document.RootElement.GetProperty("result").EnumerateArray())
            {
                folders.Add(f.GetProperty("fullPath").GetString()!);
            }
            if (document.RootElement.TryGetProperty("queryToken", out JsonElement e))
            {
                string queryToken = e.GetString()!;
                url = baseUrl + "/queryMore";
                content = new StringContent(queryToken, Encoding.UTF8, "application/text");
                atomsphereReq = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };
            }
            else
            {
                break;
            }
        } while (true);
        folders.Sort();        
        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        await response.WriteAsJsonAsync(folders);
        return response;
    }
}