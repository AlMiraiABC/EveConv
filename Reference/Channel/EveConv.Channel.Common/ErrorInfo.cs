using System.Net;

namespace EveConv.Channel.Common;

public record ErrorInfo
{
    public ErrorInfo(string message = "", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        Message = message;
        ErrorCode = (int)statusCode;
    }

    public ErrorInfo(string message = "", int errorCode = 0)
    {
        Message = message;
        ErrorCode = errorCode;
    }

    public string Message { get; }
    public int ErrorCode { get; }
}
