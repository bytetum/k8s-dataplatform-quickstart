# Migration Report: AWS S3 to Azure Blob Storage

## Summary

Successfully migrated the infrastructure configuration from AWS S3 bucket storage to Azure Blob Storage across all components in the gitops repository.

## What Was Implemented

### Storage Configuration Changes

| Component | Old Configuration | New Configuration |
|-----------|-------------------|-------------------|
| Flink Session Mode | `s3://local-rocksdb-test/...` | `abfss://flink@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net/...` |
| Flink Deployment Mode | `s3://local-rocksdb-test/...` | `abfss://flink@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net/...` |
| Polaris Iceberg Catalog | `s3://local-iceberg-test/` | `abfss://iceberg@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net/` |
| WarpStream Agent | `s3://local-warpstream-test...` | `azblob://warpstream` |
| WarpStream Schema Registry | `s3://local-warpstream-test...` | `azblob://warpstream` |
| Trino | S3 filesystem config | Azure Blob Storage filesystem config |

### Credential Changes

Replaced AWS credentials with Azure Workload Identity credentials across all components:

| Old Credential | New Credential |
|----------------|----------------|
| `AWS_ACCESS_KEY_ID` | `AZURE_STORAGE_ACCOUNT_NAME` |
| `AWS_SECRET_ACCESS_KEY` | `AZURE_TENANT_ID` |
| `AWS_REGION` | `AZURE_CLIENT_ID` |
| `AWS_ROLE_ARN` | (Removed - using Workload Identity) |

### Files Modified

**C# Pulumi Infrastructure (gitops/applications/):**
1. `infrastructure/Secrets.cs` - Updated mock credentials for Azure format
2. `flink/flink-session-mode/FlinkClusterBuilder.cs` - Azure paths and credentials
3. `flink/flink-deployment/FlinkDeploymentBuilder.cs` - Azure paths, Azure CLI init container
4. `polaris/Polaris.cs` - Azure storage detection and configuration
5. `kafkaconnect/KafkaConnectClusterBuilder.cs` - Azure credentials

**Helm Values (gitops/manifests/):**
1. `trino/values.yaml` - Azure Blob Storage filesystem configuration
2. `warpstream-agent/values.yaml` - Azure blob URL and credentials
3. `warpstream-schema-registry/values.yaml` - Azure blob URL and credentials
4. `polaris/values.yaml` - Azure storage bucket and credentials

### Container Image Changes

| Component | Old Image | New Image |
|-----------|-----------|-----------|
| Flink Deployment Init Container | `amazon/aws-cli:latest` | `mcr.microsoft.com/azure-cli:latest` |

### Command Changes

Flink deployment init container commands updated:
- Old: `aws s3 cp s3://bucket/path /local/path`
- New: `az storage blob download --account-name $AZURE_STORAGE_ACCOUNT_NAME --container-name flink --name blob-path --file /local/path --auth-mode login`

## How the Solution Was Tested

1. **Syntax Validation**: All YAML files validated for correct syntax
2. **Build Verification**: Ran `dotnet build` to verify C# code compiles (note: pre-existing unrelated build error exists)
3. **Reference Search**: Comprehensive grep searches confirmed no residual S3/AWS references in source files:
   - No `s3://` in application source code
   - No `AWS_ACCESS_KEY` in application source code
   - No `amazonaws` references in relevant files

## Challenges Encountered

1. **Pre-existing Build Error**: The `dotnet build` command fails due to a missing `Pulumi.Crds.KafkaConnect` namespace. This error existed before the migration and is unrelated to the S3→Azure changes.

2. **Generated Manifests**: Files in `gitops/manifests/*/1-manifest/` directories still contain old S3/AWS references. These are Pulumi-generated outputs that will be updated when manifests are regenerated.

3. **Multiple URL Schemes**: Azure Blob Storage supports multiple URL schemes (`abfss://`, `wasbs://`, `azblob://`). Used appropriate schemes for each component:
   - `abfss://` for Hadoop-compatible access (Flink, Polaris)
   - `azblob://` for WarpStream native Azure support

## Remaining Manual Steps

Before deploying to a live environment:

1. **Replace Placeholder Values**:
   - `PLACEHOLDER_STORAGE_ACCOUNT` → actual Azure storage account name
   - `PLACEHOLDER_TENANT_ID` → actual Azure tenant ID
   - `PLACEHOLDER_CLIENT_ID` → actual Azure client ID (for workload identity)

2. **Create Azure Resources**:
   - Azure Storage Account with hierarchical namespace enabled
   - Three containers: `flink`, `iceberg`, `warpstream`
   - Azure Workload Identity configuration for Kubernetes

3. **Regenerate Manifests**: Run Pulumi to regenerate the `1-manifest/` directory files with Azure configuration

4. **Resolve Pre-existing Issues**: Fix the `KafkaConnect` namespace error in the Pulumi project (unrelated to this migration)

## Azure Container Structure

| Container Name | Purpose |
|----------------|---------|
| `flink` | Flink checkpoints, savepoints, HA storage, completed jobs |
| `iceberg` | Polaris Iceberg catalog data |
| `warpstream` | WarpStream agent and schema registry data |

## Verification Commands

After deployment, verify the migration with:

```bash
# Check no S3 references in source
grep -r "s3://" gitops/applications/
grep -r "AWS_ACCESS_KEY" gitops/applications/

# Check Azure references are in place
grep -r "AZURE_STORAGE_ACCOUNT_NAME" gitops/applications/
grep -r "abfss://" gitops/applications/
grep -r "azblob://" gitops/manifests/
```

All searches should return expected results with Azure configurations only.
