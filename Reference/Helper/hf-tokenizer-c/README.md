# C-API for Hugging face tokenizer

> Cloned from [tokenizers-cpp](https://github.com/mlc-ai/tokenizers-cpp/blob/main/rust/).

This is a C-API for [Hugging face tokenizer](https://github.com/huggingface/tokenizers/)
to read HF format `tokenizer.json` file.

Build targets via `cross` container.

MSRV: 1.82

## Support platforms

|OS|Target|Status|
|--|------|------|
|Windows|x86_64-pc-windows-gnu|√|
|Windows|i686-pc-windows-gnu||
|Windows|aarch64-pc-windows-gnullvm|!|
|Linux(glibc)|x86_64-unknown-linux-gnu|√|
|Linux(glibc)|i686-unknown-linux-gnu|!|
|Linux(glibc)|armv7-unknown-linux-gnueabihf|!|
|Linux(glibc)|aarch64-unknown-linux-gnu|!|
|Linux(glibc)|loongarch64-unknown-linux-gnu|!|
|Linux(musl)|x86_64-unknown-linux-musl||
|Linux(musl)|i686-unknown-linux-musl||
|Linux(musl)|armv7-unknown-linux-musleabihf||
|Linux(musl)|aarch64-unknown-linux-musl||
|Linux(musl)|loongarch64-unknown-linux-gnu||

* Status: `√`-Success, `!`-NotTest, `empty`-NotWork
