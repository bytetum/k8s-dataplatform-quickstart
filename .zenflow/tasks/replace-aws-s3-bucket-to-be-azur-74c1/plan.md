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

### [ ] Step: Update Secrets Infrastructure

Modify `gitops/applications/infrastructure/Secrets.cs`:
- Update mock credentials from AWS format to Azure format
- Add new secret structure for Azure credentials

**Verification**: Build passes with `dotnet build`

---

### [ ] Step: Update Flink Session Mode

Modify `gitops/applications/flink/flink-session-mode/FlinkClusterBuilder.cs`:
- Update external secret reference from S3 to Azure
- Replace S3 paths with Azure Blob Storage paths in ConfigMap
- Replace AWS credential env vars with Azure equivalents

**Verification**:
- `dotnet build` passes
- Generated manifests show Azure paths

---

### [ ] Step: Update Flink Deployment Mode

Modify `gitops/applications/flink/flink-deployment/FlinkDeploymentBuilder.cs`:
- Change `_s3BucketPath` to Azure path
- Replace AWS CLI container with Azure CLI container
- Replace `aws s3 cp` commands with `az storage blob download`
- Update credential env vars

**Verification**:
- `dotnet build` passes
- Generated manifests show Azure CLI and commands

---

### [ ] Step: Update Polaris Iceberg Catalog

Modify `gitops/applications/polaris/Polaris.cs`:
- Change storage location from S3 to Azure
- Update storage type detection from `s3*` to Azure pattern
- Replace AWS_ROLE_ARN with Azure credentials
- Update storage config JSON for Azure

**Verification**:
- `dotnet build` passes
- Catalog creator script handles Azure paths correctly

---

### [ ] Step: Update Kafka Connect

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
