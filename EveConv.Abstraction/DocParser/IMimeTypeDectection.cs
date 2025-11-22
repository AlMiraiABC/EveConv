// https://github.com/microsoft/kernel-memory/blob/bd8d34e67dcd2b52acb408661d58b648453efbd3/service/Abstractions/Pipeline/MimeTypes.cs
// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser
{
    public interface IMimeTypeDetection
    {
        public string GetFileType(string filename);
        public bool TryGetFileType(string filename, out string? mimeType);
    }
}
