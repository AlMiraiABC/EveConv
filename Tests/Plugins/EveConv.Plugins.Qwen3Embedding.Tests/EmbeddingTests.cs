using System.Numerics.Tensors;
using Amazon.Runtime.Internal.Transform;
using EveConv.Plugins.Api;

[assembly: CaptureConsole]

namespace EveConv.Plugins.Qwen3Embedding
{
    public class EmbeddingTests : IDisposable
    {
        private const string RES_FOLDER = "Resources";

        public EmbeddingTests()
        {
            // ClearDownload();
            dotenv.net.DotEnv.Load();
        }

        public void Dispose()
        {
            // ClearDownload();
            GC.SuppressFinalize(this);
        }

        private static void ClearDownload()
        {
            if (!Directory.Exists(RES_FOLDER))
            {
                return;
            }
            try
            {
                Directory.Delete(RES_FOLDER, true);
            }
            catch
            {
                // do nothing
            }
        }

        private static float[] ReadDataSource(string filename)
        {
            var content = File.ReadAllText(Path.Combine("DataSource", filename));
            return [.. content.Split(['[', ']', ','], StringSplitOptions.RemoveEmptyEntries).Select(float.Parse)];
        }

        [Fact]
        public async Task GetEmbeddingAsync()
        {
            const string input = "Hello world!";
            var config = new PluginConfig()
            {
                { "HFToken", Environment.GetEnvironmentVariable("HF_TOKEN") },
                { "HFEndpoint", Environment.GetEnvironmentVariable("HF_ENDPOINT") }
            };
            var context = new PluginContext("./", new(), config);
            using var plugin = new Plugin(context, null!, null);
            var embedding = await plugin.GetEmbeddingAsync(input, TestContext.Current.CancellationToken);
            Assert.NotNull(embedding);
            const string filename = "embedding-1-output.json";
            var expected = ReadDataSource(filename);
            Assert.Equal(expected.Length, embedding.Vector.Length);
            var distance = TensorPrimitives.Distance(embedding.Vector.Span, expected); // 0.318974167
            var similarity = 1 - Math.Pow(distance, 2) / 2; // 0.9491277
            Console.WriteLine(distance);
            Assert.Equal(0.95, similarity, 0.5);
        }
    }
}
