using System.Numerics.Tensors;
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
            var input = new Dictionary<string, Array?>{
                {"data_0", ReadEmbedded("input.data") }
            };
            var model = ReadModel("squeezenet.onnx");
            var executor = new OnnxExecutor(new OnnxExecutorConfiguration(), null, NullLoggerFactory.Instance);
            using var session = await executor.CreateSessionAsync(model, "squeezent", TestContext.Current.CancellationToken);
            var info = await session.ExecuteAsync(input, null, TestContext.Current.CancellationToken);
            var output = info.Outputs;
            var actualData = output["softmaxout_1"]?.Cast<float>().ToArray();
            Assert.NotNull(actualData);
            var expectedData = ReadEmbedded("expected_output.data");
            Assert.Equal(expectedData.Length, actualData.Length);
            var act = Tensor.Create(actualData);
            var exp = Tensor.Create(expectedData);
            var diff = Tensor.Distance<float>(act, exp);
            Assert.True(diff < 1e-5);
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
