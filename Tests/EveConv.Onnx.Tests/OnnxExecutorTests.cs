using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;

namespace EveConv.Onnx.Tests
{
    public class OnnxExecutorTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        public OnnxExecutorTests(ITestOutputHelper output)
        {
            this._output = output;
        }

        public async ValueTask InitializeAsync()
        {
            return;
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return;
        }

        [Fact]
        public async Task Execute_Success()
        {
            var input = ReadEmbedded("input.data");
            var model = ReadModel("squeezenet.onnx");
            var executor = new OnnxExecutor(new OnnxExecutorConfiguration(), null, NullLoggerFactory.Instance);
            using var session = await executor.CreateSessionAsync(model, "squeezent", TestContext.Current.CancellationToken);
            var @params = new OnnxInferenceSessionExecuteParameter(MemoryMarshal.AsBytes(input).ToArray(), session.Instance);
            using var output = await session.ExecuteAsync(@params, null, TestContext.Current.CancellationToken);
            Assert.Single(output);
            var actualData = output[0].GetTensorDataAsSpan<float>();
            var expectedData = ReadEmbedded("expected_output.data");
            Assert.Equal(expectedData.Length, actualData.Length);
            for (int i = 0; i < actualData.Length; i++)
            {
                Assert.Equal(expectedData[i], actualData[i], 1e-5f);
            }
        }

        static float[] ReadEmbedded(string path)
        {
            // skip first row of input name.
            return [.. File.ReadAllLines(path)[1].Split([',', '[', ']'], StringSplitOptions.RemoveEmptyEntries).Select(float.Parse)];
        }

        static byte[] ReadModel(string path)
        {
            return File.ReadAllBytes(path);
        }
    }
}
