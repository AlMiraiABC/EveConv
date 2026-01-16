# permissions
mkdir -p \
  /home/ubuntu/.vscode-server \
  /home/ubuntu/.cache \
  /home/ubuntu/.nuget/packages \
  /home/ubuntu/.local/share/NuGet \
  /home/ubuntu/.cargo;
sudo chown -R ubuntu:ubuntu \
  /home/ubuntu/.vscode-server \
  /home/ubuntu/.cache \
  /home/ubuntu/.nuget \
  /home/ubuntu/.local/share \
  /home/ubuntu/.cargo \
  /workspaces/EveConv \
  || true;

# packages
cd /workspaces/EveConv;
dotnet restore;
cd /workspaces/EveConv/Reference/Helper/hf-tokenizer-c;
cargo check;
cd /workspaces/EveConvTests/Python;
uv sync;
