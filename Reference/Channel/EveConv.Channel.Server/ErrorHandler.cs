using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using NetMQ;

namespace EveConv.Channel.Server
{
    internal static class ErrorHandler
    {
        public static ErrorInfo Handle(string msg, int errorCode = 0)
        {
            return new ErrorInfo(msg, errorCode);
        }

        public static ErrorInfo Handle(Exception ex, HttpStatusCode errorCode = HttpStatusCode.InternalServerError)
        {
            return new ErrorInfo(ex.Message, (int)errorCode);
        }
        public static ErrorInfo Handle(Exception ex, int errorCode = (int)HttpStatusCode.InternalServerError)
        {
            return new ErrorInfo(ex.Message, errorCode);
        }

        public static ErrorInfo Handle(HttpRequestException ex)
        {
            return new ErrorInfo(ex.Message, ex.StatusCode ?? HttpStatusCode.InternalServerError);
        }

        #region reserved error info

        public static readonly ErrorInfo BadRequest = new(nameof(BadRequest), HttpStatusCode.BadRequest);
        public static readonly byte[] BadRequestMsgPack = BadRequest.ToMsgPack();
        public static readonly ErrorInfo Unauthorized = new(nameof(Unauthorized), HttpStatusCode.Unauthorized);
        public static readonly byte[] UnauthorizedMsgPack = Unauthorized.ToMsgPack();
        public static readonly ErrorInfo Forbidden = new(nameof(Forbidden), HttpStatusCode.Forbidden);
        public static readonly byte[] ForbiddenMsgPack = Forbidden.ToMsgPack();
        public static readonly ErrorInfo NotFound = new(nameof(NotFound), HttpStatusCode.NotFound);
        public static readonly byte[] NotFoundMsgPack = NotFound.ToMsgPack();
        public static readonly ErrorInfo InternalServerError = new(nameof(InternalServerError), HttpStatusCode.InternalServerError);
        public static readonly byte[] InternalServerErrorMsgPack = InternalServerError.ToMsgPack();
        public static readonly ErrorInfo BadGateway = new(nameof(BadGateway), HttpStatusCode.BadGateway);
        public static readonly byte[] BadGatewayMsgPack = BadGateway.ToMsgPack();
        public static readonly ErrorInfo ServiceUnavailable = new(nameof(ServiceUnavailable), HttpStatusCode.ServiceUnavailable);
        public static readonly byte[] ServiceUnavailableMsgPack = ServiceUnavailable.ToMsgPack();

        #endregion
    }


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
}
