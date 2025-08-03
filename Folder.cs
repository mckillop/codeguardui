using Microsoft.AspNetCore.Http;
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
        try
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            string atomsphereAccountId = SharedLogic.GetRequiredQueryParam(req, "atomsphereAccountId");
            string atomsphereUsername = SharedLogic.GetRequiredQueryParam(req, "atomsphereUsername");
            string atomspherePasswordEncrypted = SharedLogic.GetRequiredQueryParam(req, "atomspherePasswordEncrypted");

            string basicAuthToken = SharedLogic.AtomsphereBasicAuthToken(atomsphereUsername, atomspherePasswordEncrypted);
            string baseUrl = SharedLogic.GetAtomsphereRootUrl() + atomsphereAccountId + "/Folder";
            string url = baseUrl + "/query";
            var body = SharedLogic.GetAtomsphereFoldersRequestBody();
            string json = JsonSerializer.Serialize(body);
            HttpClient client = new();
            StringContent content = new(json, Encoding.UTF8, "application/json");
            HttpRequestMessage? atomsphereReq = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            List<string> folders = new List<string>();
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
                    foreach (var f in responseJson.RootElement.GetProperty("result").EnumerateArray())
                    {
                        folders.Add(f.GetProperty("fullPath").GetString()!);
                    }
                    /*                    if (document.RootElement.TryGetProperty("queryToken", out JsonElement e))
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
                                            keepPaginating = false;
                                        }*/
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
            folders.Sort();
            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            await response.WriteAsJsonAsync(folders);
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