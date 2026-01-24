using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction.Downloader;
using EveConv.Abstraction.ModelExecutor;
using EveConv.Abstraction.Tokenizer;
using EveConv.HuggingFaceFastTokenizer;
using EveConv.MimeType;
using EveConv.Onnx;
using Microsoft.ML.OnnxRuntime;

namespace EveConv.Downloader.Tests
{
    public class OnnxTextEmbedderTests
    {
        private readonly static string TOKENIZER_PATH = Path.Combine("Resources", "bge-small-zh-v1.5-tokenizer.json");
        private readonly static string MODEL_PATH = Path.Combine("Resources", "bge-small-zh-v1.5-model_quantized.onnx");

        private readonly IDownloader _downloader;
        private readonly ITextTokenizer<long> _tokenizer;
        private readonly IModelInference _modelInference;

        public OnnxTextEmbedderTests()
        {
            _downloader = new LocalDownloader(new MimeTypesDetection());
            _tokenizer = new HFFastTokenizer(new HFFastTokenizerConfiguration() { TokenizerJsonPath = TOKENIZER_PATH });
            _modelInference = new OnnxExecutor(new OnnxExecutorConfiguration(), _downloader);
        }

        [Fact]
        public async Task EmbeddingAsync_Short_Success()
        {
            var embedder = new BgeSmallZhV1_5Embedder(_modelInference, MODEL_PATH, _tokenizer);
            var text = "今天天气真好！";
            var embedding = await embedder.EmbeddingAsync(text, cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(embedding);
            Assert.Equal(2, embedding.Rank);
            var tokens = await embedder.GetTokensAsync(text, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(expected: tokens.Length, embedding.GetLength(0)); // dynamic
            Assert.Equal(512, embedding.GetLength(1));
        }

        [Fact]
        public async Task EmbeddingAsync_Long_OnnxRuntimeException()
        {
            // maximum input token length is 512 of model bge-small-zh-v1.5.
            var embedder = new BgeSmallZhV1_5Embedder(_modelInference, MODEL_PATH, _tokenizer);
            // 节选自《蛙》（莫言著）第一部 第七章
            var text = """
                在一九六零年下半年，也就是我们吃煤块之后不久，曾传出了姑姑即将与那个飞行员结婚的消息。为了陪嫁品的问题，大奶奶过墙来与我母亲商量，最后决定把墙外那棵百年树龄的大楸树砍倒，让乡里手艺最好的范木匠制做成家具。我确实看到父亲陪着范木匠来丈量过那棵树，那棵树因为面临着杀伐被吓得枝条颤抖，叶子哗哗，仿佛哭泣。
                但这事儿后来就没了消息，姑姑也好久没有回来了。我跑到大奶奶家去探听消息，大奶奶用拐棒毫不客气地将我打出来。我猛地发现，大奶奶老得像那些传说中的“老娘婆”一样了。
                下那年的第一场雪的早晨，太阳非常红。我们穿着草鞋上学时，感觉到了脚冷和手冷。我们在操场上奔跑喊叫，借以取暖。突然，空中传来令人惊惧的轰鸣声。我们仰脸张着嘴巴，看到有一个庞然大物——暗红色的——拖着黑色的浓烟——睁着两只红色的大眼——龇着白森森的巨齿——浑身哆嗦着——对着我们扑过来。飞机，妈呀，飞机！难道它要在我们操场上降落吗？
                我们从来没有这么近距离地看过飞机，飞机翅膀搧起的风把地上的鸡毛和枯叶卷扬起来，如果它能降落在操场上该有多好啊，我们可以近前观看，我们可以伸手摸摸它，我们如果好运气，很可能被允许钻到它的肚子里去玩玩呢，我们没准儿可以请那飞行员给我们讲几个战斗故事。他很可能是我准姑夫的战友，不，我准姑夫的”歼5”比这个黑家伙漂亮多了，因此我准姑夫不可能与开这种笨家伙的人是战友。但，怎么说呢，能开上这种飞机，也够神气了是不？把这么沉重的一块钢铁开到天上去的人，哪个会不是英雄呢？——我是没看到飞行员的脸的，但事后很多同学都信誓旦旦地说，他们透过飞机头上的玻璃，看到了飞行员的脸——那架我以为肯定要降落在我们身边的飞机似乎很不情愿地抬起了头，猛地往右一拐，肚皮擦着我们村东头那棵大杨树的梢儿，扎到村东辽阔的麦田里去了。我们听到一声巨响。这巨响比上次听到的“音爆”要粗大浑厚许多。我们感到脚下的地皮都抖起来，耳朵里嗡嗡地响着，眼睛里出现许多金星星。紧接着便有一股浓烟夹着暗红的火柱冲天而起，阳光一下子变成了紫红色，随即我们便嗅到了呛得人不能呼吸的怪味儿。
                """;
            // OnnxRunTimeException
            // [ErrorCode:RuntimeException] Non-zero status code returned while running Add node.
            // Name:'/embeddings/Add_1' Status Message: xxxxxx onnxruntime::BroadcastIterator::Append axis == 1 || axis == largest was false. Attempting to broadcast an axis by a dimension other than 1. 512 by 860
            await Assert.ThrowsAsync<OnnxRuntimeException>(async () => await embedder.EmbeddingAsync(text, cancellationToken: TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task BatchEmbeddingAsync_Short_Success()
        {
            var embedder = new BgeSmallZhV1_5Embedder(_modelInference, MODEL_PATH, _tokenizer);
            var texts = new[] { "今天天气真好！", "中国的首都是北京。" };
            var embedding = await embedder.BatchEmbeddingAsync(texts, cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(embedding);
            Assert.Equal(3, embedding.Rank);
            Assert.Equal(expected: 2, embedding.GetLength(0));
            //Assert.Equal(expected: 256, embedding.GetLength(1)); // dynamic
            Assert.Equal(512, embedding.GetLength(2));
        }

    }
}
