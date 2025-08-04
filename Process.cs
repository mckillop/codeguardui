using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Atturra.CodeGuard;

public class Process
{
    private readonly ILogger<Process> _logger;

    public Process(ILogger<Process> logger)
    {
        _logger = logger;
    }
    [Function("GetAllProcesses")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "process")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string atomsphereAccountId = SharedLogic.GetRequiredQueryParam(req, "atomsphereAccountId");
            string atomsphereUsername = SharedLogic.GetRequiredQueryParam(req, "atomsphereUsername");
            string atomspherePasswordEncrypted = SharedLogic.GetRequiredQueryParam(req, "atomspherePasswordEncrypted");

            string basicAuthToken = SharedLogic.AtomsphereBasicAuthToken(atomsphereUsername, atomspherePasswordEncrypted);
            List<FolderDto> folders = await GetAllFolders.Folders(atomsphereAccountId, basicAuthToken); // get the list of folders first, so we can look up the full path later
            string baseUrl = SharedLogic.GetAtomsphereRootUrl() + atomsphereAccountId + "/ComponentMetadata";
            string url = baseUrl + "/query";
            var body = GetAtomsphereRequestBody();
            string json = JsonSerializer.Serialize(body);
            HttpClient client = new();
            StringContent content = new(json, Encoding.UTF8, "application/json");
            HttpRequestMessage? atomsphereReq = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            List<object> processes = [];
            bool keepPaginating = true;
            do
            {
                atomsphereReq!.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthToken);
                atomsphereReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage atomsphereResponse = client.Send(atomsphereReq);
                HttpStatusCode statusCode = atomsphereResponse.StatusCode;
                string responseBody = await atomsphereResponse.Content.ReadAsStringAsync();
                if ((int)statusCode == 200)
                {
                    var responseJson = JsonDocument.Parse(responseBody);
                    foreach (JsonElement e in responseJson.RootElement.GetProperty("result").EnumerateArray())
                    {
                        string folderId = e.GetProperty("folderId").GetString()!;
                        string fullPath = folders.FirstOrDefault(f=>f.id == folderId)!.fullPath!;
                        processes.Add(
                            new
                            {
                                ComponentId = e.GetProperty("componentId").GetString(),
                                Name = e.GetProperty("name").GetString(),
                                FullPath = fullPath
                            }
                        );
                    }
                    atomsphereReq = SharedLogic.GetAtomsphereNextPage(responseJson, baseUrl);
                    if (atomsphereReq == null)
                    {
                        keepPaginating = false;
                    }
                }
                else
                {
                    throw new AppException(responseBody, statusCode);
                }
            } while (keepPaginating);
            processes = [.. processes.OrderBy(p => p.GetType().GetProperty("Name")?.GetValue(p))];
            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(processes);
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
    private static object GetAtomsphereRequestBody()
    {
        return new
        {
            QueryFilter = new
            {
                expression = new Dictionary<string, object>
                    {
                        { "operator", "and" },
                        { "nestedExpression", new[]
                            {
                                new Dictionary<string, object>
                                {
                                    { "property", "type" },
                                    { "operator", "EQUALS" },
                                    { "argument", new[] { "process" } }
                                },
                                new Dictionary<string, object>
                                {
                                    { "property", "deleted" },
                                    { "operator", "EQUALS" },
                                    { "argument", new[] { false } }
                                },
                                new Dictionary<string, object>
                                {
                                    { "property", "currentVersion" },
                                    { "operator", "EQUALS" },
                                    { "argument", new[] { true } }
                                }
                            }
                        }
                    }
            }
        };
    }    
}