using System;
using System.Collections.Generic;
using System.Text;
using Testcontainers.Minio;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace EveConv.FileStorage.S3.Tests
{
    public class MinioContainerFixture(IMessageSink messageSink)
        : ContainerFixture<MinioBuilder, MinioContainer>(messageSink)
    {
        protected override MinioBuilder Configure(MinioBuilder builder)
        {
            // default root credentials `minioadmin:minioadmin`
            return builder.WithImage("minio/minio:RELEASE.2025-09-07T16-13-09Z")
                .WithPrivileged(true)
                .WithCleanUp(true);
        }
    }
}
