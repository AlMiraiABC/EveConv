using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace EveConv.HuggingFaceFastTokenizer
{
    /// <summary>
    /// Convert string to C-style string (null-terminated)
    /// </summary>
    internal readonly unsafe struct CStr : IDisposable
    {
        public byte* Ptr { get; }
        public nuint Len { get; }

        public CStr(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            var handle = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, handle, bytes.Length);
            Marshal.WriteByte(handle + bytes.Length, 0);
            Ptr = (byte*)handle;
            Len = (nuint)bytes.Length;
        }

        public void Free()
        {
            if (Ptr is not null)
            {
                Marshal.FreeHGlobal((nint)Ptr);
            }
        }

        public void Dispose()
        {
            Free();
        }
    }
}
