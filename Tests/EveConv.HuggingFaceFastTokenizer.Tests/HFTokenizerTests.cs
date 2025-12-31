namespace EveConv.HuggingFaceFastTokenizer.Tests
{
    public class HFTokenizerTests
    {
        private readonly string TKFILE = Path.Combine(AppContext.BaseDirectory, "bert-base-uncased-tokenizer.json");

        [Fact]
        public void Encode_Success()
        {
            using var tokenizer = HFTokenizer.FromFile(TKFILE);
            var tokens = tokenizer.Encode("Hello, world!");
            var expected = new uint[] { 7592, 1010, 2088, 999 };
            Assert.True(tokens.SequenceEqual(expected));
        }

        [Fact]
        public void Decode_Success()
        {
            using var tokenizer = HFTokenizer.FromFile(TKFILE);
            var text = tokenizer.Decode([7592, 1010, 2088, 999]);
            var expected = "hello, world!";
            Assert.Equal(expected, text);
        }

        [Fact]
        public void IdToToken_Success()
        {
            using var tokenizer = HFTokenizer.FromFile(TKFILE);
            var token = tokenizer.IdToToken(7592);
            var expected = "hello";
            Assert.Equal(expected, token);
        }

        [Fact]
        public void TokenToId_Success()
        {
            using var tokenizer = HFTokenizer.FromFile(TKFILE);
            var id = tokenizer.TokenToId("hello");
            var expected = 7592u;
            Assert.Equal(expected, id);
        }

        [Fact]
        public void BatchEncode_Success()
        {
            using var tokenizer = HFTokenizer.FromFile(TKFILE);
            var inputs = new string[] { "Hello, world!", "Tokenizers are great." };
            var results = tokenizer.BatchEncode(inputs);
            var expected = new uint[][]
            {
                [7592, 1010, 2088, 999],
                [19204, 17629, 2015, 2024, 2307, 1012]
            };
            for (int i = 0; i < results.Length; i++)
            {
                Assert.True(results[i].SequenceEqual(expected[i]));
            }
        }

        [Fact]
        public void BatchDecode_Success()
        {
            using var tokenizer = HFTokenizer.FromFile(TKFILE);
            var inputs = new uint[][]
            {
                [7592, 1010, 2088, 999],
                [19204, 17629, 2015, 2024, 2307, 1012]
            };
            var results = tokenizer.BatchDecode(inputs);
            var expected = new string[]
            {
                "hello, world!",
                "tokenizers are great."
            };
            Assert.True(results.SequenceEqual(expected));
        }
    }
}
