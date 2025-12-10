using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;

namespace EveConv.Downloader
{
    public class S3Configuration : S3Helper.S3Configuration, IOptions<S3Configuration>
    {
        public S3Configuration Value => this;

        public void Valid()
        {
            // No additional validation
        }
    }
}
