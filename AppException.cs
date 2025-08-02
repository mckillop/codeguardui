using System.Net;

namespace Atturra.CodeGuard;

public class AppException : Exception
{
    public int? ErrorCode { get; }
    public HttpStatusCode? HttpErrorCode { get; }
    public AppException(string message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
    public AppException(string message, HttpStatusCode httpStatusCode) : base(message)
    {
        HttpErrorCode = httpStatusCode;
    }
}
public enum ErrorCode
{
    MissingQueryParameter = 1001
}