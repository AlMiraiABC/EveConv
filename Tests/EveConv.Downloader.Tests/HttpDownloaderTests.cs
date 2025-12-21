using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace EveConv.Downloader.Tests
{
    public class HttpDownloaderTests : IAsyncLifetime
    {
        public async ValueTask InitializeAsync()
        {
            return;
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return;
        }

        private static CancellationToken CurrentCT => TestContext.Current.CancellationToken;

        [Fact]
        public async Task DownloadAsync_Default_Success()
        {
            const string FILENAME = "testfile.txt";
            const string FILECONTENT = "Test Content";
            const string MEDIA_TYPE = MediaTypeNames.Text.Plain;
            DateTimeOffset LAST_MODIFIED = new(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(8));
            var downloader = new HttpDownloader(new HttpConfiguration(), CreateHttpClient, NullLoggerFactory.Instance);
            var file = await downloader.DownloadAsync("http://example.com/testfile.txt", CurrentCT);
            Assert.Equal(FILENAME, file.FileName);
            Assert.Equal(FILECONTENT.Length, file.FileSize);
            Assert.StartsWith(MEDIA_TYPE, file.FileType); // text/plain; charset=utf-8
            using var contentReader = new StreamReader(await file.GetStreamAsync(), Encoding.UTF8);
            var content = await contentReader.ReadLineAsync(CurrentCT);
            Assert.Equal(FILECONTENT, content);

            HttpClient CreateHttpClient(HttpHostedConfiguration _)
            {
                var handlerMock = new Mock<HttpClientHandler>(MockBehavior.Strict);
                handlerMock
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(FILECONTENT, Encoding.UTF8, MEDIA_TYPE)
                        {
                            Headers =
                            {
                                ContentLength = FILECONTENT.Length,
                                LastModified = LAST_MODIFIED,
                                ContentDisposition = new ContentDispositionHeaderValue("attachment")
                                {
                                    FileNameStar = FILENAME,
                                }
                            },
                        },
                    });
                return new HttpClient(handlerMock.Object, disposeHandler: true);
            }
        }

        [Fact]
        public async Task DownloadAsync_HostedMatched_Success()
        {
            var options = new HttpConfiguration()
            {
                Hosts =
                {
                    {@".*\.matched\.com", new()}
                }
            };
            var downloader = new HttpDownloader(options, CreateHttpClient, NullLoggerFactory.Instance);
            _ = await downloader.DownloadAsync("http://file1.matched.com/resource", CurrentCT);
            Assert.Equal(1, await downloader._urlCache.CountAsync(CurrentCT));
            var c = downloader._httpClients.First().Value;
            var c1 = await downloader._urlCache.GetAsync("file1.matched.com", CurrentCT) as HttpClient;
            Assert.Equal(c, c1);
            _ = await downloader.DownloadAsync("http://file2.matched.com/resource", CurrentCT);
            Assert.Equal(2, await downloader._urlCache.CountAsync(CurrentCT));
            var c2 = await downloader._urlCache.GetAsync("file2.matched.com", CurrentCT) as HttpClient;
            Assert.Equal(c, c2);
        }

        [Fact]
        public async Task DownloadAsync_HostedUnmatched_Success()
        {
            var options = new HttpConfiguration()
            {
                Hosts =
                {
                    {@".*\.matched\.com", new()}
                }
            };
            var downloader = new HttpDownloader(options, CreateHttpClient, NullLoggerFactory.Instance);
            _ = await downloader.DownloadAsync("http://file.unmatched.com/resource", CurrentCT);
            Assert.Equal(1, await downloader._urlCache.CountAsync(CurrentCT));
            var c = downloader._defaultClient;
            var c1 = await downloader._urlCache.GetAsync("file.unmatched.com", CurrentCT) as HttpClient;
            Assert.Equal(c, c1);
        }

        private static HttpClient CreateHttpClient(HttpHostedConfiguration _)
        {
            var handlerMock = new Mock<HttpClientHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(Guid.NewGuid().ToString()),
                });
            return new HttpClient(handlerMock.Object, disposeHandler: true);
        }

    }
}
