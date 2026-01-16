FROM ubuntu:24.04

LABEL maintainer="AlMirai"
LABEL PROJECT="EveConv"
LABEL DESCRIPTION="Development environment for EveConv project"
LABEL VERSION="0.1.0"

ARG UV_INSTALLER_GHE_BASE_URL
ARG RUSTUP_UPDATE_ROOT
ARG RUSTUP_DIST_SERVER
ARG DOTNETSDK_VER=dotnet-sdk-10.0
ARG UV_VER=0.9.25
ARG RUSTSDK_VER=1.82.0
ARG WORKDIR=/app

ENV DEBIAN_FRONTEND=noninteractive
ENV LANG=C.UTF-8
ENV PATH="/home/ubuntu/.cargo/bin:/home/ubuntu/.local/bin:${PATH}"
SHELL ["/bin/bash", "-c"]

# Basic dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      sudo \
      ca-certificates \
      net-tools \
      build-essential \
      apt-transport-https \
      software-properties-common \
      gnupg \
      curl \
      wget \
      git && \
    rm -rf /var/lib/apt/lists/* && \
    echo "ubuntu ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/ubuntu && \
    chmod 0440 /etc/sudoers.d/ubuntu

USER ubuntu

# .NET SDK
RUN sudo apt-get update && \
    sudo apt-get install -y --no-install-recommends ${DOTNETSDK_VER} && \
    sudo rm -rf /var/lib/apt/lists/*

# UV
RUN export UV_INSTALLER_GHE_BASE_URL="${UV_INSTALLER_GHE_BASE_URL}"; \
    curl -LsSf --retry 5 https://astral.sh/uv/${UV_VER}/install.sh | sh

# Rust
RUN export RUSTUP_DIST_SERVER="${RUSTUP_DIST_SERVER}"; \
    export RUSTUP_UPDATE_ROOT="${RUSTUP_UPDATE_ROOT}"; \
    curl --proto '=https' --tlsv1.2 -sSf --retry 5 https://sh.rustup.rs \
        | sh -s -- -y --default-toolchain ${RUSTSDK_VER} --profile minimal && \
    rustup component add rustfmt clippy rust-src && \
    rustup component remove rust-docs || true && \
    rm -rf /home/ubuntu/.rustup/toolchains/*/share/doc \
    /home/ubuntu/.rustup/toolchains/*/share/man


WORKDIR ${WORKDIR}
COPY --chown=ubuntu:ubuntu . ${WORKDIR}

CMD dotnet sdk check && \
    cargo --version && \
    rustup --version && \
    uv --version && \
    /bin/bash
