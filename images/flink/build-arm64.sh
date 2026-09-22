#!/usr/bin/env bash

# Build only into the local Docker image store. This helper never logs in,
# pushes, or loads an image into Kubernetes.
set -Eeuo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
image="${1:-flink-test:2.1.1}"

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
