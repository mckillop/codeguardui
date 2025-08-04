using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Atturra.CodeGuard;

public class GetAllFolders
{
    private readonly ILogger<GetAllFolders> _logger;

    public GetAllFolders(ILogger<GetAllFolders> logger)
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
            List<FolderDto> folders = await Folders(atomsphereAccountId, basicAuthToken);
            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
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
    public static async Task<List<FolderDto>> Folders(string atomsphereAccountId, string basicAuthToken)
    {
        string baseUrl = SharedLogic.GetAtomsphereRootUrl() + atomsphereAccountId + "/Folder";
        string url = baseUrl + "/query";
        var body = GetAtomsphereRequestBody();
        string json = JsonSerializer.Serialize(body);
        HttpClient client = new();
        StringContent content = new(json, Encoding.UTF8, "application/json");
        HttpRequestMessage? atomsphereReq = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        List<FolderDto> folders = [];
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
                    folders.Add(new FolderDto(f.GetProperty("id").GetString()!, f.GetProperty("fullPath").GetString()!));
                }
                ;
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
        return folders;
    }
    private static object GetAtomsphereRequestBody()
    {
        return new
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
    }    
}