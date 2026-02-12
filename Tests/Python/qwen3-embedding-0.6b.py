import os

import torch
import torch.nn.functional as F
from dotenv import load_dotenv
from huggingface_hub import snapshot_download
from torch import Tensor
from transformers import AutoModel, AutoTokenizer, Qwen2TokenizerFast, Qwen3Model

from config import *
from util import write_tensor

load_dotenv()

INFILE = "output/qwen3-embedding-inputs-info.data"
OUTFILE = "output/qwen3-embedding-outputs-info.data"
MODEL_ID = "Qwen/Qwen3-Embedding-0.6B"

local_model_dir = snapshot_download(
    repo_id=MODEL_ID,
    cache_dir=os.environ.get("HF_HOME", DEFAULT_HF_CACHE_DIR),
    local_files_only=True,
)


def last_token_pool(last_hidden_states: Tensor, attention_mask: Tensor) -> Tensor:
    left_padding = attention_mask[:, -1].sum() == attention_mask.shape[0]
    if left_padding:
        return last_hidden_states[:, -1]
    else:
        sequence_lengths = attention_mask.sum(dim=1) - 1
        batch_size = last_hidden_states.shape[0]
        return last_hidden_states[
            torch.arange(batch_size, device=last_hidden_states.device), sequence_lengths
        ]


input_texts = [
    "Hello world!",
    "中国首都是北京.",
    "Gravity is a force that attracts two bodies towards each other. It gives weight to physical objects and is responsible for the movement of planets around the sun.",
]

tokenizer: Qwen2TokenizerFast = AutoTokenizer.from_pretrained(
    local_model_dir,
    padding_side="left",
    local_files_only=True,
)
# model = AutoModel.from_pretrained("Qwen/Qwen3-Embedding-0.6B")

# We recommend enabling flash_attention_2 for better acceleration and memory saving.
model: Qwen3Model = AutoModel.from_pretrained(
    local_model_dir,
    # attn_implementation="flash_attention_2",
    dtype=torch.float16,
    local_files_only=True,
)#.cuda()

max_length = 8192

# Tokenize the input texts
batch_dict = tokenizer(
    input_texts,
    padding=True,
    truncation=True,
    max_length=max_length,
    return_tensors="pt",
)
batch_dict.to(model.device)
outputs = model(**batch_dict)
embeddings: Tensor = last_token_pool(
    outputs.last_hidden_state, batch_dict["attention_mask"]  # type: ignore
)

# normalize embeddings
embeddings = F.normalize(embeddings, p=2, dim=1)
print(embeddings)

os.remove(INFILE) if os.path.exists(INFILE) else None
os.remove(OUTFILE) if os.path.exists(OUTFILE) else None
write_tensor(INFILE, "input_ids", batch_dict["input_ids"])  # type: ignore
write_tensor(INFILE, "attention_mask", batch_dict["attention_mask"])  # type: ignore
write_tensor(OUTFILE, "last_hidden_state", outputs.last_hidden_state)  # type: ignore
write_tensor(OUTFILE, "embeddings", embeddings)
