using System;
using System.Collections.Generic;
using System.Text;
using EveConv.Onnx;
using Microsoft.Extensions.Options;

namespace EveConv.Connectors.Onnx
{
    public class OnnxEmbeddingGeneratorOptions : IOptions<OnnxEmbeddingGeneratorOptions>
    {


        OnnxEmbeddingGeneratorOptions IOptions<OnnxEmbeddingGeneratorOptions>.Value => this;

        internal void Valid()
        {
        }
    }
}
