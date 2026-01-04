import json
from typing import TextIO

import torch


def tensor_to_list(tensor: torch.Tensor) -> list:
    if tensor.dtype == torch.float16 or tensor.dtype == torch.bfloat16:
        tensor = tensor.float()
    return tensor.cpu().tolist()


def write_tensor(f: str | TextIO, name: str, tensor: torch.Tensor, **kwargs):
    sclose = False
    if isinstance(f, str):
        fw = open(f, "a", encoding="utf-8", **kwargs)
        sclose = True
    else:
        fw = f
    fw.write(f"{name}, {tensor.shape}, {tensor.dtype}\n")
    json.dump(tensor_to_list(tensor), fw, ensure_ascii=False)
    fw.write("\n")
    if sclose:
        fw.close()
