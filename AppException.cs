namespace Atturra.CodeGuard;

public class AppException : Exception
{
    public int ErrorCode { get; }

    public AppException(string message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}
public enum ErrorCode
{
    MissingQueryParameter = 1001
}