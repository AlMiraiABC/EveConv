FROM ubuntu:24.04

LABEL maintainer="AlMirai"
LABEL PROJECT="EveConv"
LABEL DESCRIPTION="Development environment for EveConv project"
LABEL VERSION="0.1.0"

ARG HTTPPROXY
ARG HTTPSPROXY
ARG NOPROXY=localhost,127.0.0.1
ARG DOTNETSDK=dotnet-sdk-10.0
ARG PYTHONSDK=python3.10
ARG RUSTSDK=1.80.0
ARG WORKDIR=/app

ENV DEBIAN_FRONTEND=noninteractive
ENV LANG=C.UTF-8
ENV http_proxy=${HTTPPROXY}
ENV https_proxy=${HTTPSPROXY}
ENV no_proxy=${NOPROXY}
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
    sudo apt-get install -y --no-install-recommends ${DOTNETSDK} && \
    sudo rm -rf /var/lib/apt/lists/*

# UV
RUN curl -LsSf --retry 5 https://astral.sh/uv/install.sh | sh

# Rust
RUN curl --proto '=https' --tlsv1.2 -sSf --retry 5 https://sh.rustup.rs \
    | sh -s -- -y --default-toolchain ${RUSTSDK} --profile minimal && \
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
