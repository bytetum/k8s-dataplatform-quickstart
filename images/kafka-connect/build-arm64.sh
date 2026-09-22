#!/usr/bin/env bash

# Build only into the local Docker image store.  This script never logs in,
# pushes, or loads an image into a Kubernetes cluster.
set -Eeuo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
image="${1:-local/kafka-connect:0.47.0-kafka-4.0.0-arm64}"

case "${image}" in
  *"@"* | "")
    printf 'Image must be a non-empty tag, not a digest reference.\n' >&2
    exit 2
    ;;
esac

command -v docker >/dev/null 2>&1 || {
  printf 'docker is required.\n' >&2
  exit 127
}

exec docker buildx build \
  --platform linux/arm64 \
  --load \
  --tag "${image}" \
  --file "${script_dir}/Dockerfile" \
  "${script_dir}"
