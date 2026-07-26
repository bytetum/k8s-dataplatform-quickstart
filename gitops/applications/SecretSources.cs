namespace applications;

internal static class SecretSources
{
    public const string Namespace = "local-secrets";
    public const string StoreName = "local-kubernetes-secret-store";
    public const string ReaderServiceAccountName = "local-secret-store-reader";

    public const string ContainerRegistryReadCredentials = "container-registry-read-credentials";
    public const string ContainerRegistryWriteCredentials = "container-registry-write-credentials";
    public const string FlinkBucketCredentials = "flink-bucket-credentials";
    public const string IcebergBucketCredentials = "iceberg-bucket-credentials";
    public const string PolarisKeyPair = "polaris-key-pair";
    public const string PolarisPostgresCredentials = "polaris-postgres-credentials";
    public const string PolarisRootPassword = "polaris-root-password";
    public const string PricefilesDatabaseCredentials = "pricefiles-db-credentials";
    public const string SchemaRegistryCredentials = "schema-registry-credentials";
    public const string WarpstreamAgentApiKey = "warpstream-agent-api-key";
    public const string WarpstreamBucketCredentials = "warpstream-bucket-credentials";
    public const string WarpstreamSchemaRegistrySecrets = "warpstream-schema-registry-secrets";
}
