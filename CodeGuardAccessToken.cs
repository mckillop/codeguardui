namespace Atturra.CodeGuard;

public class CodeGuardAccessToken
{
    public required string clientId { get; set; }
    public required string id { get; set; }
    public required string accessToken { get; set; }
    //public required int expiresIn { get; set; }
    public required string timestamp { get; set; } // ISO 8601 format as string
    public int ttl { get; set;}
};