using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Onnx
{
    public class OnnxException : Exception
    {
        public OnnxException(string? message) : base(message)
        {

        }

        public OnnxException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
