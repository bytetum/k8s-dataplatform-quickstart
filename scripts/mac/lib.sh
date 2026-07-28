#!/usr/bin/env bash

# Shared, fail-closed helpers for the Mac-local Kind workflow.

set -Eeuo pipefail

MAC_SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${MAC_SCRIPTS_DIR}/../.." && pwd)"

readonly MAC_SCRIPTS_DIR
readonly REPO_ROOT
readonly KIND_CLUSTER_NAME="dataplatform-mac"
readonly EXPECTED_CONTEXT="kind-${KIND_CLUSTER_NAME}"
readonly KIND_CONFIG="${REPO_ROOT}/Files/kind-mac.yaml"
readonly PULUMI_KEYCHAIN_SERVICE="k8s-dataplatform-quickstart-pulumi"
readonly MAC_ARGO_MANIFESTS_DIR="${REPO_ROOT}/gitops/environments/mac/manifests/argocd"

log() {
  printf '[mac-local] %s\n' "$*"
}

warn() {
  printf '[mac-local] WARNING: %s\n' "$*" >&2
}

die() {
  printf '[mac-local] ERROR: %s\n' "$*" >&2
  exit 1
}

require_command() {
  local command_name="$1"
  command -v "${command_name}" >/dev/null 2>&1 ||
    die "Required command not found: ${command_name}"
}

require_macos() {
  [[ "$(uname -s)" == "Darwin" ]] ||
    die "This workflow is only for the Mac-local environment."
}

require_native_kind() {
  local machine_arch
  local expected_go_arch
  local version_output

  require_command kind
  require_macos

  machine_arch="$(uname -m)"
  case "${machine_arch}" in
    arm64)
      expected_go_arch="arm64"
      ;;
    x86_64)
      expected_go_arch="amd64"
      ;;
    *)
      die "Unsupported Mac architecture: ${machine_arch}"
      ;;
  esac

  version_output="$(kind version 2>&1)" ||
    die "Unable to run Kind."
  [[ "${version_output}" == *"darwin/${expected_go_arch}"* ]] ||
    die "Kind is not native for this Mac (${machine_arch}). Found: ${version_output}"

  log "Native Kind detected: ${version_output}"
}

require_docker_engine() {
  local docker_os

  require_command docker
  docker_os="$(docker info --format '{{.OSType}}' 2>/dev/null)" ||
    die "Docker Desktop's engine is unavailable. Start Docker Desktop and retry."
  [[ "${docker_os}" == "linux" ]] ||
    die "Kind requires Docker Desktop's Linux engine; found '${docker_os}'."
}

profile_minimums() {
  local profile="$1"

  case "${profile}" in
    foundation)
      printf '%s %s\n' 4 8
      ;;
    operators)
      printf '%s %s\n' 4 8
      ;;
    core)
      printf '%s %s\n' 8 16
      ;;
    full)
      printf '%s %s\n' 12 24
      ;;
    *)
      die "Unknown profile '${profile}'. Use foundation, operators, core, or full."
      ;;
  esac
}

require_docker_resources() {
  local profile="$1"
  local minimums
  local minimum_cpus
  local minimum_gib
  local actual_cpus
  local actual_bytes
  local actual_gib

  require_docker_engine
  minimums="$(profile_minimums "${profile}")"
  read -r minimum_cpus minimum_gib <<<"${minimums}"
  read -r actual_cpus actual_bytes < <(
    docker info --format '{{.NCPU}} {{.MemTotal}}'
  )
  actual_gib=$((actual_bytes / 1024 / 1024 / 1024))

  if ((actual_cpus < minimum_cpus || actual_bytes < minimum_gib * 1024 * 1024 * 1024)); then
    die "Docker Desktop has ${actual_cpus} CPU(s) and about ${actual_gib} GiB RAM; profile '${profile}' requires at least ${minimum_cpus} CPU(s) and ${minimum_gib} GiB."
  fi

  log "Docker resources satisfy '${profile}': ${actual_cpus} CPU(s), about ${actual_gib} GiB RAM."
}

kind_cluster_exists() {
  kind get clusters 2>/dev/null | grep -Fxq "${KIND_CLUSTER_NAME}"
}

require_expected_context() {
  local current_context

  require_command kubectl
  require_native_kind

  kind_cluster_exists ||
    die "Kind cluster '${KIND_CLUSTER_NAME}' does not exist. Refusing to use any other cluster."

  current_context="$(kubectl config current-context 2>/dev/null || true)"
  [[ "${current_context}" == "${EXPECTED_CONTEXT}" ]] ||
    die "Current context is '${current_context:-<none>}', expected '${EXPECTED_CONTEXT}'. No Kubernetes command was run."

  # Use an explicit context even after checking the current one. This read-only
  # probe proves the dedicated context is usable without consulting another
  # cluster.
  kubectl --context "${EXPECTED_CONTEXT}" get nodes --request-timeout=10s >/dev/null ||
    die "Context '${EXPECTED_CONTEXT}' is not reachable."
}

activate_isolated_kind_kubeconfig() {
  local kubeconfig_path="$1"
  local isolated_context

  kind get kubeconfig --name "${KIND_CLUSTER_NAME}" >"${kubeconfig_path}"
  chmod 600 "${kubeconfig_path}"
  export KUBECONFIG="${kubeconfig_path}"

  isolated_context="$(kubectl config current-context 2>/dev/null || true)"
  [[ "${isolated_context}" == "${EXPECTED_CONTEXT}" ]] ||
    die "Isolated kubeconfig resolved to '${isolated_context:-<none>}' instead of '${EXPECTED_CONTEXT}'."
}

new_isolated_kubeconfig() {
  mktemp "${TMPDIR:-/tmp}/dataplatform-mac-kubeconfig.XXXXXX"
}

require_pulumi_stack() {
  local project_dir="$1"
  local stack_name="$2"

  (
    cd "${project_dir}"
    pulumi stack select "${stack_name}" --non-interactive >/dev/null
  ) || die "Pulumi stack '${stack_name}' is unavailable in ${project_dir}. Initialize/select it as described in docs/MAC-LOCAL-RUNBOOK.md."
}

load_pulumi_passphrase() {
  local passphrase

  if [[ -n "${PULUMI_CONFIG_PASSPHRASE:-}" ]]; then
    return
  fi

  require_command security
  passphrase="$(
    security find-generic-password \
      -a "${USER}" \
      -s "${PULUMI_KEYCHAIN_SERVICE}" \
      -w 2>/dev/null
  )" || die "Pulumi passphrase was not found in macOS Keychain service '${PULUMI_KEYCHAIN_SERVICE}'."
  [[ -n "${passphrase}" ]] || die "The Pulumi passphrase in macOS Keychain is empty."

  export PULUMI_CONFIG_PASSPHRASE="${passphrase}"
  unset passphrase
}

require_pulumi_context_config() {
  local project_dir="$1"
  local stack_name="$2"
  local configured_context

  configured_context="$(
    cd "${project_dir}"
    pulumi config get kube_context \
      --stack "${stack_name}" \
      --non-interactive 2>/dev/null
  )" || die "Stack '${stack_name}' in ${project_dir} has no readable kube_context."

  [[ "${configured_context}" == "${EXPECTED_CONTEXT}" ]] ||
    die "Stack '${stack_name}' has kube_context='${configured_context}', expected '${EXPECTED_CONTEXT}'."
}
