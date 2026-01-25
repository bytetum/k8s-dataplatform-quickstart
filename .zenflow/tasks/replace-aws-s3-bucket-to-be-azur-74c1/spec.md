# Technical Specification: Replace AWS S3 with Azure Blob Storage

## Task Difficulty Assessment: **Hard**

This is a complex migration task involving:
- Multiple components (Flink, Polaris, Kafka Connect, Trino, WarpStream)
- Infrastructure-as-code changes (Pulumi C#)
- Kubernetes manifest updates (YAML, Helm values)
- Credential and authentication system changes
- Cross-component coordination required

---

## Technical Context

### Language and Framework
- **Primary Language**: C# (Pulumi infrastructure-as-code)
- **Infrastructure**: Kubernetes with Pulumi CRDs
- **Manifest Format**: YAML (Kubernetes ConfigMaps, Helm values)
- **Secret Management**: External Secrets Operator with ClusterSecretStore

### Key Dependencies
- Apache Flink (session mode and deployment mode)
- Apache Polaris (Iceberg catalog)
- Apache Trino (query engine)
- Kafka Connect (Strimzi operator)
- WarpStream (Kafka-compatible agent and schema registry)

---

## Current S3 Usage Analysis

### Three S3 Buckets in Use

| Bucket Name | Purpose | Components Using It |
|-------------|---------|---------------------|
| `s3://local-rocksdb-test` | Flink state storage (checkpoints, savepoints, HA, completed jobs) | Flink Session Mode, Flink Deployment |
| `s3://local-iceberg-test` | Iceberg table storage (catalog data) | Polaris, Trino, Kafka Connect |
| `s3://local-warpstream-test` | WarpStream data storage | WarpStream Agent, WarpStream Schema Registry |

### Credential Systems

| Secret Name | Keys | Used By |
|-------------|------|---------|
| `flink-bucket-credentials` | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` | Flink components |
| `iceberg-bucket-credentials` | `AWS_ACCESS_KEY`, `AWS_SECRET_KEY`, `AWS_ROLE_ARN`, `AWS_REGION` | Polaris, Kafka Connect, Trino |
| `warpstream-bucket-credentials` | `SCALEWAY_ACCESS_KEY`, `SCALEWAY_SECRET_KEY` (mapped to AWS vars) | WarpStream |

---

## Implementation Approach

### User Decisions (Confirmed)

1. **Storage Account & Container Names**: Use placeholders (to be filled in later)
2. **Authentication Method**: **Managed Identity** (Azure Workload Identity for Kubernetes)
3. **WarpStream Azure Support**: **Confirmed** - WarpStream has native built-in support for Azure Blob Storage

### Azure Blob Storage URL Formats by Component

| Component | URL Format | Example |
|-----------|------------|---------|
| **Flink** | `abfss://<container>@<storage-account>.dfs.core.windows.net/<path>` | `abfss://flink@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net/flink-session-mode/checkpoints` |
| **Polaris/Iceberg** | `abfss://<container>@<storage-account>.dfs.core.windows.net/` | `abfss://iceberg@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net/` |
| **WarpStream** | `azblob://<container-name>` | `azblob://PLACEHOLDER_WARPSTREAM_CONTAINER` |
| **Trino** | Uses Azure Blob filesystem config | N/A (configured via properties) |

### WarpStream Specific Configuration

WarpStream uses the `azblob://` URL scheme (not `az://` or `abfss://`).

**Required Environment Variables for WarpStream:**
- `AZURE_STORAGE_ACCOUNT` - Storage account name
- For Managed Identity: Use Azure Workload Identity annotations

**Service Account Annotation for Azure Workload Identity:**
```yaml
serviceAccount:
  annotations:
    "azure.workload.identity/client-id": "<managed-identity-client-id>"
```

### Proposed Bucket Mapping

| S3 Bucket | Azure Container | URL Format |
|-----------|-----------------|------------|
| `s3://local-rocksdb-test` | `flink` | `abfss://flink@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net` |
| `s3://local-iceberg-test` | `iceberg` | `abfss://iceberg@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net` |
| `s3://local-warpstream-test` | `warpstream` | `azblob://warpstream` |

### Credential Changes (Managed Identity)

Since we're using Managed Identity, the credential approach changes:

| Component | Old (AWS) | New (Azure Managed Identity) |
|-----------|-----------|------------------------------|
| **Flink** | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` | Azure Workload Identity (service account annotation) |
| **WarpStream** | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` | `AZURE_STORAGE_ACCOUNT` + Workload Identity |
| **Polaris** | `AWS_ROLE_ARN` | Azure Workload Identity |
| **Trino** | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION` | Azure Managed Identity config in catalog |
| **Kafka Connect** | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION` | Azure Workload Identity |

### Placeholders to be Replaced

The following placeholders will be used throughout the implementation:
- `PLACEHOLDER_STORAGE_ACCOUNT` - Azure storage account name
- `PLACEHOLDER_WARPSTREAM_CONTAINER` - WarpStream container name (default: `warpstream`)
- `PLACEHOLDER_MANAGED_IDENTITY_CLIENT_ID` - Azure Managed Identity client ID

---

## Source Code Changes

### 1. C# Pulumi Files (gitops/applications)

#### 1.1 `flink/flink-session-mode/FlinkClusterBuilder.cs`
**Lines to modify**: 115, 292-299, 399-420, 532-554, 641-662

Changes:
- Line 115: Update external secret key from `id:flink-s3-credentials-secret` to Azure equivalent
- Lines 292-299: Replace `s3://local-rocksdb-test/flink-session-mode/...` with Azure paths
- Lines 399-420, 532-554, 641-662: Replace `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` env vars with Azure credentials

#### 1.2 `flink/flink-deployment/FlinkDeploymentBuilder.cs`
**Lines to modify**: 25, 177-188, 225-246, 273, 277-299, 306-307

Changes:
- Line 25: Change `_s3BucketPath` from `s3://local-rocksdb-test` to Azure path
- Lines 177-188: Flink configuration paths are interpolated from `_s3BucketPath`
- Line 273: Replace `amazon/aws-cli:latest` with `mcr.microsoft.com/azure-cli:latest`
- Lines 277-299: Replace AWS credential env vars with Azure
- Lines 306-307: Replace `aws s3 cp` commands with `az storage blob download`

#### 1.3 `polaris/Polaris.cs`
**Lines to modify**: 55, 76-85, 108-122

Changes:
- Line 55: Change `s3://local-iceberg-test/` to Azure path
- Lines 76-85: Replace `AWS_ROLE_ARN` env var with Azure credential
- Lines 108-122: Update storage type detection from `s3*` to Azure pattern, update storage config JSON

#### 1.4 `kafkaconnect/KafkaConnectClusterBuilder.cs`
**Lines to modify**: 204-206, 278-280

Changes:
- Replace `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION` with Azure equivalents

#### 1.5 `infrastructure/Secrets.cs`
**Lines to modify**: 39-42

Changes:
- Update mock credentials structure from AWS to Azure format

---

### 2. Kubernetes Manifest Files (gitops/manifests)

#### 2.1 `flink-session-mode/1-manifest/v1-configmap-flink-kubernetes-operator-flink-config.yaml`
**Lines to modify**: 16, 20, 22, 23

Changes:
- Replace all `s3://local-rocksdb-test/flink-session-mode/...` paths with Azure paths

#### 2.2 `polaris/values.yaml`
**Lines to modify**: 10, 15-30

Changes:
- Line 10: Replace `AWS_STORAGE_BUCKET` with Azure storage path
- Lines 15-30: Replace AWS credential env vars with Azure equivalents

#### 2.3 `polaris/1-manifest/batch_v1-job-polaris-polaris-catalog-creator.yaml`
**Lines to modify**: 34-39, 105, 108-122

Changes:
- Line 105: Replace S3 storage location value
- Lines 34-39, 108-122: Update shell script for Azure path validation and storage config

#### 2.4 `warpstream-agent/values.yaml`
**Lines to modify**: 8, 13-22, 31

Changes:
- Line 8: Replace S3 bucket URL with Azure URL
- Lines 13-22: Replace AWS credentials with Azure credentials
- Line 31: Remove or update IAM role ARN

#### 2.5 `warpstream-schema-registry/values.yaml`
**Lines to modify**: 10, 15-24

Changes:
- Line 10: Replace S3 bucket URL with Azure URL
- Lines 15-24: Replace AWS credentials with Azure credentials

#### 2.6 `trino/values.yaml`
**Lines to modify**: 16-30, 55-57

Changes:
- Lines 16-30: Replace AWS credential env vars with Azure
- Lines 55-57: Replace S3 filesystem config with Azure Blob Storage config

---

## Data Model / API / Interface Changes

### New Secret Structure

The secrets should be restructured to use Azure credentials:

```json
{
  "AZURE_STORAGE_ACCOUNT_NAME": "<storage-account-name>",
  "AZURE_STORAGE_ACCOUNT_KEY": "<storage-account-key>",
  "AZURE_STORAGE_CONNECTION_STRING": "<full-connection-string>"
}
```

For Polaris (Iceberg catalog), Azure may require:
```json
{
  "AZURE_TENANT_ID": "<tenant-id>",
  "AZURE_CLIENT_ID": "<client-id>",
  "AZURE_CLIENT_SECRET": "<client-secret>"
}
```

### External Secret Updates

Update external secret references:
- `id:flink-s3-credentials-secret` → `id:flink-azure-credentials-secret`
- `id:c2f85be8-7fd0-402d-8229-6de987bcbbb4` (iceberg bucket) → Azure equivalent

---

## Verification Approach

### Pre-Implementation Checks
1. Verify Azure storage account and containers are created
2. Confirm Azure credentials are available in secret store

### Post-Implementation Verification
1. **Build Verification**:
   ```bash
   cd gitops && dotnet build
   ```

2. **Pulumi Preview** (if applicable):
   ```bash
   pulumi preview
   ```

3. **Manifest Generation**:
   - Verify generated YAML manifests contain Azure paths instead of S3 paths
   - Check no residual AWS references remain

4. **Search for Residual S3 References**:
   ```bash
   grep -r "s3://" gitops/
   grep -r "AWS_ACCESS_KEY" gitops/
   grep -r "amazonaws" gitops/
   ```

---

## Risk Assessment

### High Risk Areas
1. **Polaris Iceberg Catalog**: Storage type detection logic must be updated carefully
2. **Flink Init Container**: Command replacement from AWS CLI to Azure CLI
3. **WarpStream Compatibility**: Verify WarpStream supports Azure Blob Storage

### Mitigation
- Test each component independently after changes
- Keep backup of original files
- Document rollback procedure

---

## Resolved Questions

1. **Azure Storage Account Name**: Using placeholder `PLACEHOLDER_STORAGE_ACCOUNT` (to be filled in)
2. **Container Names**: Using logical names: `flink`, `iceberg`, `warpstream`
3. **Authentication Method**: **Managed Identity** (Azure Workload Identity for Kubernetes)
4. **WarpStream Azure Support**: **Confirmed** - WarpStream has native support for Azure Blob Storage using `azblob://` URL scheme ([Source: WarpStream Docs](https://docs.warpstream.com/warpstream/agent-setup/different-object-stores))

---

## Summary

This migration requires changes to **12 files**:
- 5 C# Pulumi files
- 7 YAML manifest files

Key transformation patterns:
1. Replace `s3://` URLs with `abfss://` or `az://` URLs
2. Replace AWS credential environment variables with Azure equivalents
3. Replace AWS CLI container and commands with Azure CLI equivalents
4. Update storage type detection logic in Polaris
5. Update Trino filesystem configuration for Azure
