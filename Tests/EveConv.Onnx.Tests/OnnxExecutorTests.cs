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
            using var session = await executor.CreateSession(model, "squeezent", TestContext.Current.CancellationToken);
            var (names, inputData) = ReadData(session, input);
            using var output = session.Run(new(), names, inputData, session.OutputNames);
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

        static (List<string> Names, List<OrtValue> Values) ReadData(InferenceSession session, float[] input)
        {
            var meta = session.InputMetadata;
            var names = new List<string>(meta.Count);
            var values = new List<OrtValue>(meta.Count);
            foreach (var name in meta.Keys)
            {
                var shape = Array.ConvertAll(meta[name].Dimensions, Convert.ToInt64);
                var value = OrtValue.CreateTensorValueFromMemory(input, shape);
                names.Add(name);
                values.Add(value);
            }
            return (names, values);
        }
    }
}
