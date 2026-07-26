#!/usr/bin/env bash

set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

stack_name="mac-local"

usage() {
  cat <<'EOF'
Usage: scripts/mac/preview-bootstrap.sh [--stack NAME]

Runs a non-mutating Pulumi preview of the Argo CD infrastructure against an
isolated kubeconfig containing only kind-dataplatform-mac.
EOF
}

while (($# > 0)); do
  case "$1" in
    --stack)
      (($# >= 2)) || die "--stack requires a value."
      stack_name="$2"
      shift 2
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

require_command pulumi
require_command dotnet
load_pulumi_passphrase
require_expected_context
require_pulumi_stack "${REPO_ROOT}/infrastructure" "${stack_name}"
require_pulumi_context_config "${REPO_ROOT}/infrastructure" "${stack_name}"

kubeconfig_path="$(new_isolated_kubeconfig)"
trap 'rm -f "${kubeconfig_path}"' EXIT
activate_isolated_kind_kubeconfig "${kubeconfig_path}"

dotnet build "${REPO_ROOT}/infrastructure" --nologo
(
  cd "${REPO_ROOT}/infrastructure"
  pulumi preview \
    --stack "${stack_name}" \
    --diff \
    --non-interactive \
    --suppress-outputs
)
