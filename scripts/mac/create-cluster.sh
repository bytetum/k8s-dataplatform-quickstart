#!/usr/bin/env bash

set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

profile="core"
node_image="${KIND_NODE_IMAGE:-kindest/node:v1.35.5@sha256:ce977ae6d65918d0b58a5f8b5e940429c2ce42fa3a5619ec2bbc60b949c0ac95}"
dry_run=false

usage() {
  cat <<'EOF'
Usage: scripts/mac/create-cluster.sh [options]

Options:
  --profile foundation|operators|core|full  Docker resource gate (default: core)
  --image IMAGE                   Explicit kindest/node image
                                  (default: Kubernetes v1.35.5, pinned by digest)
  --dry-run                       Print the exact create command only
  -h, --help                      Show this help

This script can only create the cluster named kind. It never deletes, recreates,
or otherwise operates on an existing Kind cluster.
EOF
}

while (($# > 0)); do
  case "$1" in
    --profile)
      (($# >= 2)) || die "--profile requires a value."
      profile="$2"
      shift 2
      ;;
    --image)
      (($# >= 2)) || die "--image requires a value."
      node_image="$2"
      shift 2
      ;;
    --dry-run)
      dry_run=true
      shift
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      die "Unknown argument: $1"
      ;;
  esac
done

[[ -f "${KIND_CONFIG}" ]] || die "Kind config not found: ${KIND_CONFIG}"
[[ "${node_image}" == kindest/node:v* || "${node_image}" == kindest/node@sha256:* ]] ||
  die "--image must be an explicit kindest/node version tag or sha256 digest."

require_native_kind
require_docker_resources "${profile}"

if kind_cluster_exists; then
  die "Cluster '${KIND_CLUSTER_NAME}' already exists. Refusing to recreate or mutate it."
fi

create_command=(
  kind create cluster
  --name "${KIND_CLUSTER_NAME}"
  --config "${KIND_CONFIG}"
  --image "${node_image}"
  --wait 5m
)

if [[ "${dry_run}" == "true" ]]; then
  printf 'Would run:'
  printf ' %q' "${create_command[@]}"
  printf '\n'
  exit 0
fi

log "Creating only Kind cluster '${KIND_CLUSTER_NAME}' with ${node_image}."
"${create_command[@]}"

current_context="$(kubectl config current-context 2>/dev/null || true)"
[[ "${current_context}" == "${EXPECTED_CONTEXT}" ]] ||
  die "Cluster creation returned, but current context is '${current_context:-<none>}', expected '${EXPECTED_CONTEXT}'."

require_expected_context
log "Cluster is ready. Existing clusters were not modified."
