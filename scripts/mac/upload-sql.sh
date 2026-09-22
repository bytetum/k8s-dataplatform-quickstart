#!/usr/bin/env bash

set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib.sh
source "${SCRIPT_DIR}/lib.sh"

readonly ENV_FILE="${LOCAL_SECRETS_FILE:-${REPO_ROOT}/.local-secrets/kind.env}"
readonly SQL_SOURCE_DIR="${REPO_ROOT}/gitops/environments/kind/sql"
readonly SQL_BUCKET="local-rocksdb-test"
readonly SQL_PREFIX="kind-local/sql"
readonly AWS_CLI_IMAGE="amazon/aws-cli:2.17.29@sha256:78276aac1fdd1c0ec198d911046777b2e5eaf4d1ff76f58e70992c9409d801ff"

temp_dir=""
cleanup() {
  [[ -z "${temp_dir}" || ! -d "${temp_dir}" ]] || rm -rf -- "${temp_dir}"
}
trap cleanup EXIT

require_command docker
require_command python3
require_docker_engine
[[ -f "${ENV_FILE}" && -r "${ENV_FILE}" ]] || die "Local secret environment file is missing or unreadable."
[[ "$(stat -f '%Lp' "${ENV_FILE}")" == *00 ]] || die "Local secret environment file must use mode 600."
[[ -d "${SQL_SOURCE_DIR}" ]] || die "Tracked kind-local SQL directory is missing."

temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/kind-local-sql.XXXXXX")"
chmod 700 "${temp_dir}"
mkdir -m 700 "${temp_dir}/rendered"

python3 - "${ENV_FILE}" "${SQL_SOURCE_DIR}" "${temp_dir}/rendered" "${temp_dir}/aws.env" <<'PY'
from pathlib import Path
import sys

env_path, source_dir, output_dir, docker_env_path = map(Path, sys.argv[1:])
values: dict[str, str] = {}
for line_number, raw in enumerate(env_path.read_text().splitlines(), 1):
    line = raw.strip()
    if not line or line.startswith("#"):
        continue
    if "=" not in raw:
        raise SystemExit(f"invalid environment line {line_number}")
    key, value = raw.split("=", 1)
    if not key or key in values:
        raise SystemExit(f"invalid or duplicate environment key on line {line_number}")
    values[key] = value

required = (
    "SCHEMA_REGISTRY_USERNAME",
    "SCHEMA_REGISTRY_PASSWORD",
    "FLINK_AWS_ACCESS_KEY_ID",
    "FLINK_AWS_SECRET_ACCESS_KEY",
    "ICEBERG_AWS_REGION",
)
missing = [key for key in required if not values.get(key)]
if missing:
    raise SystemExit("missing required local secret keys: " + ", ".join(missing))

user_info = f"{values['SCHEMA_REGISTRY_USERNAME']}:{values['SCHEMA_REGISTRY_PASSWORD']}"
sql_user_info = user_info.replace("'", "''")
for source in sorted(source_dir.glob("*.sql")):
    rendered = source.read_text().replace("@@SCHEMA_REGISTRY_USER_INFO@@", sql_user_info)
    if "@@" in rendered:
        raise SystemExit(f"unresolved template token in {source.name}")
    target = output_dir / source.name
    target.write_text(rendered)
    target.chmod(0o600)

if not any(output_dir.glob("*.sql")):
    raise SystemExit("no SQL templates found")

docker_env_path.write_text(
    "AWS_ACCESS_KEY_ID=" + values["FLINK_AWS_ACCESS_KEY_ID"] + "\n"
    "AWS_SECRET_ACCESS_KEY=" + values["FLINK_AWS_SECRET_ACCESS_KEY"] + "\n"
    "AWS_DEFAULT_REGION=" + values["ICEBERG_AWS_REGION"] + "\n"
)
docker_env_path.chmod(0o600)
PY

docker run --rm \
  --env-file "${temp_dir}/aws.env" \
  --volume "${temp_dir}/rendered:/sql:ro" \
  "${AWS_CLI_IMAGE}" \
  s3 cp /sql/ "s3://${SQL_BUCKET}/${SQL_PREFIX}/" --recursive --only-show-errors

for sql_file in "${temp_dir}"/rendered/*.sql; do
  object_key="${SQL_PREFIX}/$(basename "${sql_file}")"
  docker run --rm \
    --env-file "${temp_dir}/aws.env" \
    "${AWS_CLI_IMAGE}" \
    s3api head-object --bucket "${SQL_BUCKET}" --key "${object_key}" >/dev/null
  log "Verified s3://${SQL_BUCKET}/${object_key}."
done

log "Uploaded only the reviewed kind-local SQL prefix; legacy object keys were not modified."
