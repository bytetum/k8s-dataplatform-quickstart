#!/usr/bin/env bash

set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

stack_name="mac-local"
apply_confirmed=false

usage() {
  cat <<'EOF'
Usage: scripts/mac/bootstrap.sh --yes [--stack NAME]

Previews and then applies the infrastructure Pulumi stack to install Argo CD.
--yes is mandatory. The active context must be exactly kind-dataplatform-mac,
and Pulumi receives an isolated kubeconfig containing no AKS or legacy context.
EOF
}

while (($# > 0)); do
  case "$1" in
    --stack)
      (($# >= 2)) || die "--stack requires a value."
      stack_name="$2"
      shift 2
      ;;
    --yes)
      apply_confirmed=true
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

[[ "${apply_confirmed}" == "true" ]] ||
  die "Bootstrap changes the Mac-local cluster. Run preview-bootstrap.sh first, then pass --yes."

require_command pulumi
require_command dotnet
configure_dotnet_runtime
load_pulumi_passphrase
require_expected_context
require_pulumi_stack "${REPO_ROOT}/infrastructure" "${stack_name}"
require_pulumi_context_config "${REPO_ROOT}/infrastructure" "${stack_name}"

# Run a fresh preview through the same guarded entry point immediately before
# apply. The apply phase repeats the context check and uses a new isolated file.
"${SCRIPT_DIR}/preview-bootstrap.sh" --stack "${stack_name}"
require_expected_context

kubeconfig_path="$(new_isolated_kubeconfig)"
trap 'rm -f "${kubeconfig_path}"' EXIT
activate_isolated_kind_kubeconfig "${kubeconfig_path}"

(
  cd "${REPO_ROOT}/infrastructure"
  pulumi up \
    --stack "${stack_name}" \
    --yes \
    --diff \
    --non-interactive \
    --suppress-outputs
)

log "Argo CD bootstrap completed on ${EXPECTED_CONTEXT}."
