#!/usr/bin/env bash

set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

project="all"
argocd_stack="kind-local"
applications_stack="kind-local"
write_confirmed=false

usage() {
  cat <<'EOF'
Usage: scripts/mac/render-manifests.sh --yes [options]

Options:
  --project argocd|applications|all
  --argocd-stack NAME              (default: kind-local)
  --applications-stack NAME        (default: kind-local; clusterless render)
  --yes                            Required: allow manifest files to be rewritten
  -h, --help

The script requires the active context to be exactly kind-kind.
Render providers are clusterless and write YAML only; they do not apply workloads.
EOF
}

while (($# > 0)); do
  case "$1" in
    --project)
      (($# >= 2)) || die "--project requires a value."
      project="$2"
      shift 2
      ;;
    --argocd-stack)
      (($# >= 2)) || die "--argocd-stack requires a value."
      argocd_stack="$2"
      shift 2
      ;;
    --applications-stack)
      (($# >= 2)) || die "--applications-stack requires a value."
      applications_stack="$2"
      shift 2
      ;;
    --yes)
      write_confirmed=true
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

case "${project}" in
  argocd | applications | all) ;;
  *) die "Unknown project '${project}'. Use argocd, applications, or all." ;;
esac

[[ "${write_confirmed}" == "true" ]] ||
  die "Rendering rewrites tracked manifests. Review Git status, then pass --yes."

require_command pulumi
require_command dotnet
require_command jq
configure_dotnet_runtime
require_target_cluster

render_project() {
  local project_dir="$1"
  local stack_name="$2"
  local context_config_required="$3"

  require_pulumi_stack "${project_dir}" "${stack_name}"
  if [[ "${context_config_required}" == "true" ]]; then
    require_pulumi_context_config "${project_dir}" "${stack_name}"
  fi
  log "Building ${project_dir#${REPO_ROOT}/}."
  dotnet build "${project_dir}" --nologo
  log "Rendering stack '${stack_name}' from ${project_dir#${REPO_ROOT}/}."
  (
    cd "${project_dir}"
    pulumi up \
      --stack "${stack_name}" \
      --yes \
      --skip-preview \
      --non-interactive \
      --suppress-outputs
  )
}

clean_kind_argocd_manifests() {
  local configured_path
  local resolved_path

  configured_path="$(
    cd "${REPO_ROOT}/gitops/argocd"
    pulumi config get manifests_path \
      --stack "${argocd_stack}" \
      --non-interactive
  )" || die "Unable to read manifests_path from Argo stack '${argocd_stack}'."

  resolved_path="$(
    cd "${REPO_ROOT}/gitops/argocd"
    mkdir -p "${configured_path}"
    cd "${configured_path}"
    pwd -P
  )"

  [[ "${resolved_path}" == "${KIND_ARGO_MANIFESTS_DIR}" ]] ||
    die "Refusing to clean unexpected Argo manifest path '${resolved_path}'."

  find "${resolved_path}" -mindepth 1 -delete
  log "Cleared the dedicated kind-local Argo manifest directory before profile rendering."
}

reset_kind_argocd_render_stack() {
  local unexpected_types

  unexpected_types="$(
    cd "${REPO_ROOT}/gitops/argocd"
    pulumi stack export \
      --stack "${argocd_stack}" \
      --show-secrets=false |
      jq -r '.deployment.resources[]?.type' |
      sort -u |
      awk '
        $0 != "pulumi:pulumi:Stack" &&
        $0 != "pulumi:providers:kubernetes" &&
        $0 != "manifests" &&
        $0 != "kubernetes:argoproj.io/v1alpha1:Application" {
          print
        }
      '
  )"

  [[ -z "${unexpected_types}" ]] ||
    die "Argo render stack contains non-render resources; refusing to reset it: ${unexpected_types}"

  log "Resetting the clusterless Argo render stack so every selected Application is rewritten."
  (
    cd "${REPO_ROOT}/gitops/argocd"
    pulumi destroy \
      --stack "${argocd_stack}" \
      --yes \
      --skip-preview \
      --non-interactive \
      --suppress-outputs
  )
}

if [[ "${project}" == "argocd" || "${project}" == "all" ]]; then
  load_pulumi_passphrase
  require_pulumi_stack "${REPO_ROOT}/gitops/argocd" "${argocd_stack}"
  require_pulumi_context_config "${REPO_ROOT}/gitops/argocd" "${argocd_stack}"
  reset_kind_argocd_render_stack
  clean_kind_argocd_manifests
  render_project "${REPO_ROOT}/gitops/argocd" "${argocd_stack}" true
fi

if [[ "${project}" == "applications" || "${project}" == "all" ]]; then
  load_pulumi_passphrase
  render_project "${REPO_ROOT}/gitops/applications" "${applications_stack}" false
fi

log "Render complete. Review git diff before committing or bootstrapping."
