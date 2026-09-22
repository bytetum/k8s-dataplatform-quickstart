using System;

namespace applications;

public static class Constants
{
    // mac-local is the default so a future render of that stack keeps the
    // legacy namespaces, topics, and object paths.  The kind-local stack opts
    // in through applications:profile.
    internal static bool IsKindLocal { get; private set; }

    internal static void UseProfile(string? profile)
    {
        IsKindLocal = string.Equals(profile, "kind-local", StringComparison.Ordinal);
    }

    public static string IsolationPrefix => IsKindLocal ? "kind-local" : "";

    // Polaris / Iceberg
    public static string PolarisCatalog => IsKindLocal ? "lakehouse_kind_local" : "ao_catalog";
    public static string PolarisDatabase => IsKindLocal ? "kind_local" : "test_db";
    public static string PolarisNamespace => IsKindLocal ? "lakehouse-polaris" : "polaris";
    public static string IcebergBucketPath => IsKindLocal
        ? "s3://local-iceberg-test/kind-local"
        : "s3://local-iceberg-test";
    public static string PolarisUri =>
        $"http://polaris.{PolarisNamespace}.svc.cluster.local:8181/api/catalog";

    // WarpStream (Helm releases)
    public static string WarpStreamNamespace => IsKindLocal ? "lakehouse-warpstream" : "warpstream";
    public const string WarpStreamAgentRelease = "warpstream";
    public const string SchemaRegistryRelease = "warpstream-schema-registry";
    public const int KafkaPort = 9092;
    public const int SchemaRegistryPort = 9094;

    public static string KafkaBootstrapServers =>
        $"{WarpStreamAgentRelease}-agent.{WarpStreamNamespace}.svc.cluster.local:{KafkaPort}";

    public static string SchemaRegistryUrl =>
        $"http://{SchemaRegistryRelease}-warpstream-agent.{WarpStreamNamespace}.svc.cluster.local:{SchemaRegistryPort}";

    // Kafka Connect
    public static string KafkaConnectNamespace => IsKindLocal ? "lakehouse-kafka-connect" : "kafka-connect";
    public static string KafkaConnectClusterName => IsKindLocal ? "kind-local-kafka-connect" : "m3-kafka-connect";

    public static string KafkaTopic(string topic)
    {
        if (!IsKindLocal || string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(IsolationPrefix))
        {
            return topic;
        }

        return topic.StartsWith($"{IsolationPrefix}.", StringComparison.Ordinal)
            ? topic
            : $"{IsolationPrefix}.{topic}";
    }

    public static string KafkaGroup(string group)
    {
        if (!IsKindLocal || string.IsNullOrEmpty(group) || string.IsNullOrEmpty(IsolationPrefix))
        {
            return group;
        }

        return group.Equals(IsolationPrefix, StringComparison.Ordinal)
            || group.StartsWith($"{IsolationPrefix}-", StringComparison.Ordinal)
            ? group
            : $"{IsolationPrefix}-{group}";
    }

    // Trino
    public static string TrinoNamespace => IsKindLocal ? "lakehouse-trino" : "trino";

    // S3 / Object Storage
    public static string S3BucketPath => IsKindLocal
        ? "s3://local-rocksdb-test/kind-local"
        : "s3://local-rocksdb-test";

    public static string SqlObjectUri(string fileName) => IsKindLocal
        ? $"{S3BucketPath}/sql/{fileName}"
        : $"{S3BucketPath}/{fileName}";

    public static string FlinkNamespace => IsKindLocal ? "lakehouse-flink" : "flink-kubernetes-operator";
    public static string FlinkServiceAccount => IsKindLocal
        ? "lakehouse-flink-sql-gateway-sa"
        : "flink-sql-gateway-sa";
    public static string FlinkSessionClusterId => IsKindLocal
        ? "kind-local-flink-session-cluster"
        : "flink-session-cluster-01";
    public static string SecretNamespace => IsKindLocal ? "lakehouse-secrets" : "local-secrets";

    // Marquez (OpenLineage backend)
    public static string MarquezNamespace => IsKindLocal ? "lakehouse-marquez" : "marquez";
    public const int MarquezApiPort = 80;
    public const int MarquezWebPort = 3000;
    public static string MarquezApiUrl =>
        $"http://marquez.{MarquezNamespace}.svc.cluster.local:{MarquezApiPort}";

    // OpenMetadata
    public static string OpenMetadataNamespace => IsKindLocal ? "lakehouse-openmetadata" : "openmetadata";
    public const int OpenMetadataPort = 8585;
    public static string OpenMetadataUrl =>
        $"http://openmetadata.{OpenMetadataNamespace}.svc.cluster.local:{OpenMetadataPort}";
}
