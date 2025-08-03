using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using PgpCore;

namespace Atturra.CodeGuard;

public class PgpDecrypt
{
    private readonly ILogger<PgpDecrypt> _logger;

    public PgpDecrypt(ILogger<PgpDecrypt> logger)
    {
        _logger = logger;
    }

    [Function("PgpDecrypt")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pgpDecrypt")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            // get inputs
            string? requestBody = await req.ReadAsStringAsync();
            JsonElement requestBodyRoot = JsonDocument.Parse(requestBody).RootElement;
            string base64EncryptedMessage = requestBodyRoot.GetProperty("base64EncryptedMessage").GetString()!;
            string base64EncryptedKey = requestBodyRoot.GetProperty("base64EncryptedPgpPrivateKey").GetString()!;

            // do the decryption
            string pgpPrivateKey = Encoding.UTF8.GetString(Convert.FromBase64String(base64EncryptedKey));
            string pgpMessage = Encoding.UTF8.GetString(Convert.FromBase64String(base64EncryptedMessage));
            EncryptionKeys encryptionKeys = new EncryptionKeys(pgpPrivateKey, "");
            PGP pgp = new PGP(encryptionKeys);
            string decryptedMessage = pgp.Decrypt(pgpMessage);

            // return the data
            var responseBody = new
            {
                decryptedMessage = decryptedMessage
            };
            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(responseBody);
        return response;
    }
        catch (Exception ex)
        {
            return await SharedLogic.ExceptionAsync(req, ex);
}
    }
}