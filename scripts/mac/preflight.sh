#!/usr/bin/env bash

set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

profile="core"
check_context=true

usage() {
  cat <<'EOF'
Usage: scripts/mac/preflight.sh [--profile foundation|operators|core|full] [--for-create]

Checks native Mac tooling and Docker Desktop resources. By default it also
requires the active, reachable context to be exactly kind-kind.
Use --for-create only before the dedicated cluster has been created.
EOF
}

while (($# > 0)); do
  case "$1" in
    --profile)
      (($# >= 2)) || die "--profile requires a value."
      profile="$2"
      shift 2
      ;;
    --for-create)
      check_context=false
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

require_command kubectl
require_command pulumi
require_command dotnet
require_native_kind
require_docker_resources "${profile}"

if [[ "${check_context}" == "true" ]]; then
  require_target_cluster
  kubeconfig_path="$(new_isolated_kubeconfig)"
  trap 'rm -f "${kubeconfig_path}"' EXIT
  activate_isolated_kind_kubeconfig "${kubeconfig_path}"
  log "Isolated context guard passed: ${EXPECTED_CONTEXT}"
else
  log "Creation preflight passed; Kubernetes context checks were intentionally skipped."
fi
