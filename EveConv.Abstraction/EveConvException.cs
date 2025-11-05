using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EveConv.Abstraction
{
    public class EveConvException : Exception
    {
        public EveConvException()
        {
        }

        public EveConvException(string? message) : base(message)
        {
        }

        public EveConvException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
