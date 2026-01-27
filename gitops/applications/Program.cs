global using Pulumi;
global using Kubernetes = Pulumi.Kubernetes;
using applications.flink.flink_deployment;
using applications.flink.flink_session_mode;
using applications.infrastructure;
using applications.warpstream;
using applications.polaris;
using applications.postgres;
using applications.kafkaconnect;

return await Deployment.RunAsync(() =>
{
    var infrastructure = new Infrastructure("../manifests");
    var warpstream = new Warpstream("../manifests");
    var WarpstreamSchemaRegistry = new WarpstreamSchemaRegistry("../manifests");
    var polaris = new Polaris("../manifests");
    var postgres = new Postgres("../manifests");
    var kafkaConnect = new KafkaConnect("../manifests");
    var postgreDebeziumConnector = new PostgresDebeziumConnector("../manifests");

    // Kafka Connect Cluster
    var kafkaConnectCluster = new KafkaConnectClusterBuilder("../manifests", "m3-kafka-connect")
        .WithBootstrapServers("warpstream-agent.warpstream.svc.cluster.local:9092")
        .WithImage("ttl.sh/hxt-kafka-connect-amd64-20-12:24h")
        .WithReplicas(1)
        .WithMetricsConfig("kafka-connect-metrics", "metrics-config.yml")
        .WithResources(
            cpuRequest: "2",
            memoryRequest: "4Gi",
            cpuLimit: "2",
            memoryLimit: "8Gi",
            jvmMaxHeap: "5G") // Keep heap at ~50% of container limit for JVM overhead
        .Build();

    // ========================================================================
    // BRONZE LAYER CONNECTORS (DD130 Naming)
    // Schema Compatibility: NONE (accept any schema from source systems)
    // ========================================================================
    var icebergSinkCidmas = new IcebergSinkConnectorBuilder("../manifests")
        .WithNaming(NamingConventionHelper.DataLayer.Bronze, domain: "m3", dataset: "cidmas")
        .WithIdColumns("idsuno")
        .WithPartitionBy("idcono")
        .Build();

    var icebergSinkCidven = new IcebergSinkConnectorBuilder("../manifests")
        .WithNaming(NamingConventionHelper.DataLayer.Bronze, domain: "m3", dataset: "cidven")
        .WithIdColumns("iisuno")
        .WithPartitionBy("iisugr")
        .Build();

    var icebergSinkCsytab = new IcebergSinkConnectorBuilder("../manifests")
        .WithNaming(NamingConventionHelper.DataLayer.Bronze, domain: "m3", dataset: "csytab")
        .WithIdColumns("ctstky", "ctstco")
        .WithPartitionBy("ctstco")
        .Build();

    // Debug Connector for Analyst Development
    // Regex-based dynamic routing for debug.silver.* topics
    // Fail-fast mode with 10s commits for immediate feedback
    // See: docs/DEVOPS-ANALYST-WAY-OF-WORKING.md
    var debugIcebergSinkSilver = new IcebergSinkConnectorBuilder("../manifests")
        .WithConnectorPrefix("debug-iceberg-sink")
        .WithConnectorName("silver")
        .WithTopicsRegex(@"^debug\.silver\..*")
        .WithDynamicRouting("iceberg_table")
        .WithDefaultIdColumns("record_key")
        .WithCommitInterval(10000) // 10s for fast feedback
        .WithSchemaRegistryCache(cacheSize: 1000, cacheTtlMs: 300000)
        .WithFailFastMode(retryDelayMaxMs: 60000, retryTimeoutMs: 300000)
        .Build();

    // ========================================================================
    // SILVER LAYER CONNECTORS (DD130 Naming)
    // Schema Compatibility: BACKWARD (controlled evolution, consumers update first)
    // Auto-derived: topic, table, DLQ names from DD130 components
    // ========================================================================
    var icebergSinkValidExample = new IcebergSinkConnectorBuilder("../manifests")
        .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "valid_example")
        .WithIdColumns("record_key")
        .Build();

    var icebergSinkCustomerTransactions = new IcebergSinkConnectorBuilder("../manifests")
        .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "customer_transactions")
        .WithIdColumns("record_key")
        .Build();

    // var icebergSinkProductInventory = new IcebergSinkConnectorBuilder("../manifests")
    //     .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "product_inventory")
    //     .WithIdColumns("record_key")
    //     .Build();

    // ========================================================================
    // FLINK JOBS (DD130 Naming)
    // Auto-derived deployment name: {layer}.{domain}.{dataset}
    // ========================================================================
    var scriptFlinkDeployment = new FlinkDeploymentBuilder("../manifests")
        .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "valid_example")
        .WithSqlS3Uri("s3://local-rocksdb-test/schema_validator_test.sql")
        .WithUpgradeMode(FlinkDeploymentBuilder.UpgradeMode.LastState)
        .Build();

    var scriptFlinkDeployment2 = new FlinkDeploymentBuilder("../manifests")
        .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "customer_transactions")
        .WithSqlS3Uri("s3://local-rocksdb-test/schema_validator_test_2.sql")
        .WithUpgradeMode(FlinkDeploymentBuilder.UpgradeMode.LastState)
        .Build();

    // var scriptFlinkDeployment3 = new FlinkDeploymentBuilder("../manifests")
    //     .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "product_inventory")
    //     .WithSqlS3Uri("s3://local-rocksdb-test/schema_validator_test_3.sql")
    //     .WithUpgradeMode(FlinkDeploymentBuilder.UpgradeMode.Stateless)
    //     .Build();

    var flinkSessionMode = new FlinkClusterBuilder("../manifests")
        .WithTaskSlots(2)
        .WithTaskManagerReplicas(2)
        .WithParallelismDefault(2)
        .WithJobManagerMemory("1024m")
        .WithTaskManagerMemory("2048m")
        .Build();
});