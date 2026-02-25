namespace applications;

public static class Constants
{
    // Polaris / Iceberg
    public const string PolarisCatalog = "ao_catalog";
    public const string PolarisDatabase = "test_db";
    public const string PolarisNamespace = "polaris";
    public static string PolarisUri =>
        $"http://polaris.{PolarisNamespace}.svc.cluster.local:8181/api/catalog";

    // WarpStream (Helm releases)
    public const string WarpStreamNamespace = "warpstream";
    public const string WarpStreamAgentRelease = "warpstream";
    public const string SchemaRegistryRelease = "warpstream-schema-registry";
    public const int KafkaPort = 9092;
    public const int SchemaRegistryPort = 9094;

    public static string KafkaBootstrapServers =>
        $"{WarpStreamAgentRelease}-agent.{WarpStreamNamespace}.svc.cluster.local:{KafkaPort}";

    public static string SchemaRegistryUrl =>
        $"http://{SchemaRegistryRelease}-warpstream-agent.{WarpStreamNamespace}.svc.cluster.local:{SchemaRegistryPort}";

    // Kafka Connect
    public const string KafkaConnectNamespace = "kafka-connect";

    // S3 / Object Storage
    public const string S3BucketPath = "s3://local-rocksdb-test";

    // Marquez (OpenLineage backend)
    public const string MarquezNamespace = "marquez";
    public const int MarquezApiPort = 5000;
    public const int MarquezWebPort = 3000;
    public static string MarquezApiUrl =>
        $"http://marquez.{MarquezNamespace}.svc.cluster.local:{MarquezApiPort}";

    // OpenMetadata
    public const string OpenMetadataNamespace = "openmetadata";
    public const int OpenMetadataPort = 8585;
    public static string OpenMetadataUrl =>
        $"http://openmetadata.{OpenMetadataNamespace}.svc.cluster.local:{OpenMetadataPort}";
}