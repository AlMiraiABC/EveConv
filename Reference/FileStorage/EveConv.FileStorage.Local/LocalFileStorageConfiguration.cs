using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;

namespace EveConv.FileStorage.Local
{
    public class LocalFileStorageConfiguration : IOptions<LocalFileStorageConfiguration>
    {
        /// <summary>
        /// Root directory path on local machine to store files.
        /// </summary>
        public string RootPath { get; init; } = "./files";

        public LocalFileStorageConfiguration Value => this;
    }
}
