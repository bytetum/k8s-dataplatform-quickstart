# Spec and build

## Configuration
- **Artifacts Path**: {@artifacts_path} → `.zenflow/tasks/{task_id}`

---

## Agent Instructions

Ask the user questions when anything is unclear or needs their input. This includes:
- Ambiguous or incomplete requirements
- Technical decisions that affect architecture or user experience
- Trade-offs that require business context

Do not make assumptions on important decisions — get clarification first.

---

## Workflow Steps

### [x] Step: Technical Specification
<!-- chat-id: b76c6b67-09f7-4602-b210-dafbb9e5b33f -->

**Completed**: Created technical specification in `spec.md`

**Difficulty Assessment**: Hard
- Multiple components (Flink, Polaris, Kafka Connect, Trino, WarpStream)
- 12 files requiring modifications across C# Pulumi and YAML manifests
- Cross-component credential and storage path coordination

**Summary**: Migration from AWS S3 to Azure Blob Storage requires:
- Replacing 3 S3 buckets with Azure containers
- Updating credential systems from AWS to Azure
- Modifying storage type detection logic in Polaris
- Replacing AWS CLI with Azure CLI in init containers

---

### [x] Step: Clarify Azure Configuration

**Resolved:**
1. **Storage account name**: Use placeholder `PLACEHOLDER_STORAGE_ACCOUNT` (to be filled in)
2. **Container names**: `flink`, `iceberg`, `warpstream`
3. **Authentication**: **Managed Identity** (Azure Workload Identity)
4. **WarpStream**: **Confirmed** - Native Azure Blob Storage support using `azblob://` URL scheme

---

### [x] Step: Update Secrets Infrastructure
<!-- chat-id: aa6b03c8-a5c1-4d71-bc7c-d13181170f31 -->

Modify `gitops/applications/infrastructure/Secrets.cs`:
- Update mock credentials from AWS format to Azure format
- Add new secret structure for Azure credentials

**Verification**: Build passes with `dotnet build`

**Completed**: Updated `Secrets.cs` with the following changes:
- Changed iceberg bucket credentials (`id:c2f85be8-7fd0-402d-8229-6de987bcbbb4`) from AWS format to Azure format
- Changed warpstream credentials (`id:827b85c8-babe-4a43-8af2-dce1dd530081`) from Scaleway/AWS format to Azure format
- Added new Flink Azure credentials secret (`id:flink-azure-credentials-secret`)
- All Azure credentials now use: `AZURE_STORAGE_ACCOUNT_NAME`, `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` placeholders

**Note**: Build has pre-existing errors unrelated to this change (KafkaConnect namespace missing). Secrets.cs compiles without errors.

---

### [x] Step: Update Flink Session Mode
<!-- chat-id: c6fb789f-47ae-4c32-a825-7b0637d8a033 -->

Modify `gitops/applications/flink/flink-session-mode/FlinkClusterBuilder.cs`:
- Update external secret reference from S3 to Azure
- Replace S3 paths with Azure Blob Storage paths in ConfigMap
- Replace AWS credential env vars with Azure equivalents

**Verification**:
- `dotnet build` passes
- Generated manifests show Azure paths

**Completed**: Updated `FlinkClusterBuilder.cs` with the following changes:
- Changed external secret key from `id:flink-s3-credentials-secret` to `id:flink-azure-credentials-secret`
- Replaced S3 paths with Azure Blob Storage paths using `abfss://flink@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net/flink-session-mode/...` format for:
  - checkpoints
  - savepoints
  - completed-jobs
  - high-availability storage
- Replaced AWS credential env vars (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`) with Azure equivalents (`AZURE_STORAGE_ACCOUNT_NAME`, `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`) in:
  - JobManager deployment
  - TaskManager deployment
  - SQL Gateway deployment

**Note**: Build has pre-existing errors unrelated to this change (KafkaConnect namespace missing). FlinkClusterBuilder.cs compiles without errors.

---

### [x] Step: Update Flink Deployment Mode
<!-- chat-id: 771e02e3-4bb6-4b01-a547-0074b84c989b -->

Modify `gitops/applications/flink/flink-deployment/FlinkDeploymentBuilder.cs`:
- Change `_s3BucketPath` to Azure path
- Replace AWS CLI container with Azure CLI container
- Replace `aws s3 cp` commands with `az storage blob download`
- Update credential env vars

**Verification**:
- `dotnet build` passes
- Generated manifests show Azure CLI and commands

**Completed**: Updated `FlinkDeploymentBuilder.cs` with the following changes:
- Renamed `_s3BucketPath` to `_azureBlobPath` and changed value from `s3://local-rocksdb-test` to `abfss://flink@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net`
- Updated all Flink configuration paths (checkpoints, savepoints, HA storage, completed-jobs) to use Azure Blob Storage format
- Replaced `amazon/aws-cli:latest` container image with `mcr.microsoft.com/azure-cli:latest`
- Replaced AWS credential env vars (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`) with Azure equivalents (`AZURE_STORAGE_ACCOUNT_NAME`, `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`) in:
  - Main flink container
  - Init container
- Replaced `aws s3 cp` commands with `az storage blob download --account-name $AZURE_STORAGE_ACCOUNT_NAME --container-name flink --auth-mode login`
- Added helper method `GetBlobName()` to extract blob name from Azure paths

**Note**: Build has pre-existing errors unrelated to this change (KafkaConnect namespace missing). FlinkDeploymentBuilder.cs compiles without errors.

---

### [x] Step: Update Polaris Iceberg Catalog
<!-- chat-id: 62b34514-33bd-4761-bf97-7fc8f43f6194 -->

Modify `gitops/applications/polaris/Polaris.cs`:
- Change storage location from S3 to Azure
- Update storage type detection from `s3*` to Azure pattern
- Replace AWS_ROLE_ARN with Azure credentials
- Update storage config JSON for Azure

**Verification**:
- `dotnet build` passes
- Catalog creator script handles Azure paths correctly

**Completed**: Updated `Polaris.cs` with the following changes:
- Changed storage location from `s3://local-iceberg-test/` to `abfss://iceberg@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net/`
- Replaced `AWS_ROLE_ARN` env var with Azure credentials (`AZURE_STORAGE_ACCOUNT_NAME`, `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`)
- Updated storage type detection in shell script to support Azure patterns (`abfss://`, `wasbs://`, `az://`) in addition to S3
- Updated storage config JSON to add `tenantId` for Azure storage type instead of `roleArn` for S3

**Note**: Build has pre-existing errors unrelated to this change (KafkaConnect namespace missing). Polaris.cs compiles without errors.

---

### [ ] Step: Update Kafka Connect
<!-- chat-id: 61eabc4b-9f53-47e2-92b4-9ddf7d4c2f8d -->

Modify `gitops/applications/kafkaconnect/KafkaConnectClusterBuilder.cs`:
- Replace AWS credential env vars with Azure equivalents

**Verification**: `dotnet build` passes

---

### [ ] Step: Update Trino Manifest

Modify `gitops/manifests/trino/values.yaml`:
- Replace AWS credential env vars with Azure
- Replace S3 filesystem config with Azure Blob Storage config

**Verification**: Valid YAML syntax, no S3 references

---

### [ ] Step: Update WarpStream Manifests

Modify:
- `gitops/manifests/warpstream-agent/values.yaml`
- `gitops/manifests/warpstream-schema-registry/values.yaml`

Changes:
- Replace S3 bucket URLs with Azure URLs
- Replace AWS credentials with Azure credentials
- Remove IAM role ARN

**Verification**: Valid YAML syntax, no S3 references

---

### [ ] Step: Update Polaris Helm Values

Modify `gitops/manifests/polaris/values.yaml`:
- Replace AWS_STORAGE_BUCKET with Azure storage path
- Replace AWS credential env vars with Azure equivalents

**Verification**: Valid YAML syntax, no AWS references

---

### [ ] Step: Final Verification

1. Run `dotnet build` in gitops directory
2. Search for residual S3/AWS references:
   - `grep -r "s3://" gitops/`
   - `grep -r "AWS_ACCESS_KEY" gitops/`
   - `grep -r "amazonaws" gitops/`
3. Verify all generated manifests use Azure paths
4. Document any remaining items that need manual updates

---

### [ ] Step: Write Report

Write completion report to `report.md`:
- What was implemented
- How the solution was tested
- Challenges encountered
- Any remaining manual steps needed
