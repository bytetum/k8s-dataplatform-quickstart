#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
secrets_dir="${repo_root}/.local-secrets"
if [[ -n "${LOCAL_SECRETS_FILE:-}" ]]; then
    env_file="${LOCAL_SECRETS_FILE}"
elif [[ -f "${secrets_dir}/kind.env" ]]; then
    env_file="${secrets_dir}/kind.env"
elif [[ -f "${secrets_dir}/mac.env" ]]; then
    env_file="${secrets_dir}/mac.env"
else
    env_file="${secrets_dir}/kind.env"
fi
readonly expected_context="kind-kind"
source_namespace="lakehouse-secrets"
profile="core"

fail() {
    printf 'Error: %s\n' "$1" >&2
    exit 1
}

usage() {
    cat <<'EOF'
Usage: scripts/mac/seed-secrets.sh [--profile PROFILE]

Seed only the local source Secrets required by the selected kind-local profile.

Profiles:
  foundation  No workload source Secrets.
  operators   No workload source Secrets.
  core        WarpStream, WarpStream Schema Registry, Polaris/Postgres, and Iceberg.
  query-lineage  Core requirements; Trino and Marquez add no source inputs.
  processing  Core plus Flink source Secrets.
  integration Core plus Flink and Kafka Connect source Secrets.
  full        Integration requirements plus dedicated OpenMetadata and Airflow inputs.

The script accepts only the kind-kind context, never displays secret
values, and requires the local environment file and referenced PEM files to be
private (no group or other permissions).
EOF
}

check_private_file() {
    local path="$1"
    local mode

    [[ -f "${path}" && -r "${path}" ]] || fail "a required local secret file is missing or unreadable"
    mode="$(stat -f '%Lp' "${path}")"
    case "${mode}" in
        *00) ;;
        *) fail "local secret files must not grant group or other permissions (use chmod 600)" ;;
    esac
}

get_value() {
    local key="$1"

    awk -v wanted="${key}" '
        index($0, wanted "=") == 1 {
            value = substr($0, length(wanted) + 2)
            sub(/\r$/, "", value)
            print value
            found = 1
            exit
        }
        END {
            if (!found) {
                exit 2
            }
        }
    ' "${env_file}"
}

get_required_value() {
    local key="$1"
    local value

    value="$(get_value "${key}")" || fail "${key} is missing from the local environment file"
    [[ -n "${value}" ]] || fail "${key} must not be empty"
    printf '%s' "${value}"
}

resolve_secret_file() {
    local configured_path="$1"
    local resolved_path

    if [[ "${configured_path}" = /* ]]; then
        resolved_path="${configured_path}"
    else
        resolved_path="${secrets_dir}/${configured_path}"
    fi

    check_private_file "${resolved_path}"
    printf '%s' "${resolved_path}"
}

seed_literal_secret() {
    local secret_name="$1"
    local secret_dir="${temp_dir}/${secret_name}"
    local key
    local value
    local value_file
    local -a kubectl_args
    shift

    # Flink and Kafka Connect both stage schema-registry-credentials in one run.
    [[ "${secret_dir}" == "${temp_dir}/"* ]] || fail "refusing to stage a secret outside the temporary directory"
    rm -rf -- "${secret_dir}"
    mkdir -m 700 "${secret_dir}"
    kubectl_args=()

    while (( "$#" )); do
        key="$1"
        value="$2"
        value_file="${secret_dir}/${key}"
        printf '%s' "${value}" > "${value_file}"
        chmod 600 "${value_file}"
        kubectl_args+=("--from-file=${key}=${value_file}")
        shift 2
    done

    kubectl --context "${expected_context}" create secret generic "${secret_name}" \
        --namespace "${source_namespace}" \
        --type Opaque \
        "${kubectl_args[@]}" \
        --dry-run=client \
        --output yaml |
        kubectl --context "${expected_context}" apply --filename - >/dev/null

    printf 'Seeded %s/%s\n' "${source_namespace}" "${secret_name}"
}

seed_file_secret() {
    local secret_name="$1"
    local public_key_file="$2"
    local private_key_file="$3"

    kubectl --context "${expected_context}" create secret generic "${secret_name}" \
        --namespace "${source_namespace}" \
        --type Opaque \
        "--from-file=public.pem=${public_key_file}" \
        "--from-file=private.pem=${private_key_file}" \
        --dry-run=client \
        --output yaml |
        kubectl --context "${expected_context}" apply --filename - >/dev/null

    printf 'Seeded %s/%s\n' "${source_namespace}" "${secret_name}"
}

parse_args() {
    while (( "$#" )); do
        case "$1" in
            --profile)
                (( $# >= 2 )) || fail "--profile requires a value"
                profile="$2"
                shift 2
                ;;
            --help|-h)
                usage
                exit 0
                ;;
            *)
                fail "unknown argument: $1 (use --help)"
                ;;
        esac
    done

    case "${profile}" in
        foundation|operators|core|query|query-lineage|query_lineage|processing|integration|full|heavy-metadata|heavy_metadata) ;;
        *) fail "invalid profile: ${profile} (use --help)" ;;
    esac
}

seed_core_secrets() {
    local iceberg_access_key iceberg_secret_key iceberg_role_arn iceberg_region
    local polaris_public_key_file polaris_private_key_file
    local polaris_db_address polaris_db_username polaris_db_password polaris_root_password
    local schema_registry_username schema_registry_password warpstream_schema_registry_agent_key
    local warpstream_agent_key warpstream_bucket_access_key warpstream_bucket_secret_key

    iceberg_access_key="$(get_required_value ICEBERG_AWS_ACCESS_KEY)"
    iceberg_secret_key="$(get_required_value ICEBERG_AWS_SECRET_KEY)"
    iceberg_role_arn="$(get_value ICEBERG_AWS_ROLE_ARN)" ||
        fail "ICEBERG_AWS_ROLE_ARN is missing from the local environment file"
    iceberg_region="$(get_required_value ICEBERG_AWS_REGION)"
    polaris_public_key_file="$(resolve_secret_file "$(get_required_value POLARIS_PUBLIC_KEY_FILE)")"
    polaris_private_key_file="$(resolve_secret_file "$(get_required_value POLARIS_PRIVATE_KEY_FILE)")"
    polaris_db_address="$(get_required_value POLARIS_DB_ADDRESS)"
    polaris_db_username="$(get_required_value POLARIS_DB_USERNAME)"
    polaris_db_password="$(get_required_value POLARIS_DB_PASSWORD)"
    polaris_root_password="$(get_required_value POLARIS_ROOT_PASSWORD)"
    schema_registry_username="$(get_required_value SCHEMA_REGISTRY_USERNAME)"
    schema_registry_password="$(get_required_value SCHEMA_REGISTRY_PASSWORD)"
    warpstream_schema_registry_agent_key="$(get_required_value WARPSTREAM_SCHEMA_REGISTRY_AGENT_KEY)"
    warpstream_agent_key="$(get_required_value WARPSTREAM_AGENT_KEY)"
    warpstream_bucket_access_key="$(get_required_value WARPSTREAM_BUCKET_ACCESS_KEY)"
    warpstream_bucket_secret_key="$(get_required_value WARPSTREAM_BUCKET_SECRET_KEY)"

    seed_literal_secret "iceberg-bucket-credentials" \
        "AWS_ACCESS_KEY" "${iceberg_access_key}" \
        "AWS_SECRET_KEY" "${iceberg_secret_key}" \
        "AWS_ROLE_ARN" "${iceberg_role_arn}" \
        "AWS_REGION" "${iceberg_region}"
    seed_file_secret "polaris-key-pair" "${polaris_public_key_file}" "${polaris_private_key_file}"
    seed_literal_secret "polaris-postgres-credentials" \
        "db-address" "${polaris_db_address}" \
        "username" "${polaris_db_username}" \
        "password" "${polaris_db_password}"
    seed_literal_secret "polaris-root-password" "polaris-root-password" "${polaris_root_password}"
    seed_literal_secret "warpstream-agent-api-key" "agent_key" "${warpstream_agent_key}"
    seed_literal_secret "warpstream-bucket-credentials" \
        "SCALEWAY_ACCESS_KEY" "${warpstream_bucket_access_key}" \
        "SCALEWAY_SECRET_KEY" "${warpstream_bucket_secret_key}"
    seed_literal_secret "warpstream-schema-registry-secrets" \
        "agent_key" "${warpstream_schema_registry_agent_key}" \
        "USERNAME" "${schema_registry_username}" \
        "PASSWORD" "${schema_registry_password}"
}

seed_flink_secrets() {
    local flink_access_key flink_secret_key

    flink_access_key="$(get_required_value FLINK_AWS_ACCESS_KEY_ID)"
    flink_secret_key="$(get_required_value FLINK_AWS_SECRET_ACCESS_KEY)"

    seed_literal_secret "flink-bucket-credentials" \
        "AWS_ACCESS_KEY_ID" "${flink_access_key}" \
        "AWS_SECRET_ACCESS_KEY" "${flink_secret_key}"
    seed_literal_secret "schema-registry-credentials" \
        "username" "$(get_required_value SCHEMA_REGISTRY_USERNAME)" \
        "password" "$(get_required_value SCHEMA_REGISTRY_PASSWORD)"
}

seed_kafka_connect_secrets() {
    local pricefiles_db_host pricefiles_db_port pricefiles_db_username pricefiles_db_password pricefiles_db_name

    pricefiles_db_host="$(get_required_value PRICEFILES_DB_HOST)"
    pricefiles_db_port="$(get_required_value PRICEFILES_DB_PORT)"
    pricefiles_db_username="$(get_required_value PRICEFILES_DB_USERNAME)"
    pricefiles_db_password="$(get_required_value PRICEFILES_DB_PASSWORD)"
    pricefiles_db_name="$(get_required_value PRICEFILES_DB_NAME)"

    seed_literal_secret "pricefiles-db-credentials" \
        "host" "${pricefiles_db_host}" \
        "port" "${pricefiles_db_port}" \
        "username" "${pricefiles_db_username}" \
        "password" "${pricefiles_db_password}" \
        "dbname" "${pricefiles_db_name}"
    seed_literal_secret "schema-registry-credentials" \
        "username" "$(get_required_value SCHEMA_REGISTRY_USERNAME)" \
        "password" "$(get_required_value SCHEMA_REGISTRY_PASSWORD)"
}

seed_marquez_secrets() {
    seed_literal_secret "marquez-database-credentials" \
        "password" "$(get_required_value MARQUEZ_DB_PASSWORD)"
}

seed_openmetadata_secrets() {
    seed_literal_secret "openmetadata-database-credentials" \
        "root-password" "$(get_required_value OPENMETADATA_MYSQL_ROOT_PASSWORD)" \
        "password" "$(get_required_value OPENMETADATA_MYSQL_PASSWORD)"
    seed_literal_secret "openmetadata-airflow-credentials" \
        "password" "$(get_required_value OPENMETADATA_AIRFLOW_PASSWORD)" \
        "connection" "$(get_required_value OPENMETADATA_AIRFLOW_METADATA_CONNECTION)"
}

parse_args "$@"

command -v kubectl >/dev/null 2>&1 || fail "kubectl is not installed"
command -v kind >/dev/null 2>&1 || fail "kind is not installed"
check_private_file "${env_file}"

awk '
    /^[[:space:]]*($|#)/ { next }
    /^[A-Z][A-Z0-9_]*=/ {
        key = $0
        sub(/=.*/, "", key)
        if (seen[key]++) {
            printf "duplicate key: %s\n", key > "/dev/stderr"
            exit 1
        }
        next
    }
    {
        printf "invalid line %d in local environment file\n", NR > "/dev/stderr"
        exit 1
    }
' "${env_file}" || fail "the local environment file is invalid"

kind get clusters 2>/dev/null | grep -Fxq "kind" ||
    fail "kind cluster 'kind' does not exist"

target_kubeconfig="$(mktemp "${TMPDIR:-/tmp}/kind-local-secrets-kubeconfig.XXXXXX")"
chmod 600 "${target_kubeconfig}"
temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/dataplatform-secrets.XXXXXX")"
chmod 700 "${temp_dir}"
cleanup() {
    if [[ -n "${target_kubeconfig:-}" && -f "${target_kubeconfig}" ]]; then
        rm -f -- "${target_kubeconfig}"
    fi
    if [[ -n "${temp_dir:-}" && -d "${temp_dir}" ]]; then
        rm -rf -- "${temp_dir}"
    fi
}
trap cleanup EXIT

kind get kubeconfig --name kind >"${target_kubeconfig}" ||
    fail "unable to read the kind-kind kubeconfig"
chmod 600 "${target_kubeconfig}"

isolated_context="$(KUBECONFIG="${target_kubeconfig}" kubectl config current-context 2>/dev/null || true)"
[[ "${isolated_context}" == "${expected_context}" ]] ||
    fail "isolated kubeconfig resolved to ${isolated_context:-<none>} instead of ${expected_context}"
export KUBECONFIG="${target_kubeconfig}"

kubectl --context "${expected_context}" get namespace "${source_namespace}" >/dev/null ||
    fail "namespace ${source_namespace} does not exist; apply the reviewed GitOps foundation first"

case "${profile}" in
    foundation|operators)
        printf 'No workload source Secrets are required for the %s profile.\n' "${profile}"
        ;;
    core)
        seed_core_secrets
        ;;
    query|query-lineage|query_lineage)
        seed_core_secrets
        seed_marquez_secrets
        ;;
    processing)
        seed_core_secrets
        seed_flink_secrets
        ;;
    integration)
        seed_core_secrets
        seed_flink_secrets
        seed_kafka_connect_secrets
        ;;
    full|heavy-metadata|heavy_metadata)
        seed_core_secrets
        seed_flink_secrets
        seed_kafka_connect_secrets
        seed_marquez_secrets
        seed_openmetadata_secrets
        ;;
esac

printf 'Local source Secrets for the %s profile are seeded without displaying their values.\n' "${profile}"
