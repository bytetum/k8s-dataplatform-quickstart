global using Pulumi;
global using Kubernetes = Pulumi.Kubernetes;
using applications;
using applications.flink.flink_deployment;
using applications.flink.flink_session_mode;
using applications.infrastructure;
using applications.warpstream;
using applications.polaris;
using applications.postgres;
using applications.kafkaconnect;
using applications.trino;
using applications.marquez;
using applications.openmetadata;

return await Deployment.RunAsync(() =>
{
    var config = new Config();
    // Select the profile before any resource is constructed.  Field
    // initializers read Constants, and the default profile is mac-local.
    Constants.UseProfile(config.Get("profile"));
    // Keep the legacy mac-local render root as the safe default.  The
    // kind-local stack supplies an explicit isolated root under
    // gitops/environments/kind so it never rewrites the legacy manifests.
    var manifestsRoot = config.Get("manifests_root") ?? "../manifests";

    var infrastructure = new Infrastructure(
        manifestsRoot,
        renderCertManager: !(config.GetBoolean("reuse_existing_operators") ?? false));
    var warpstream = new Warpstream(manifestsRoot);
    var WarpstreamSchemaRegistry = new WarpstreamSchemaRegistry(manifestsRoot);
    var polaris = new Polaris(manifestsRoot);
    var postgres = new Postgres(manifestsRoot);
    var kafkaConnect = new KafkaConnect(manifestsRoot);
    var trino = new Trino(manifestsRoot);
    if (Constants.IsKindLocal)
    {
        _ = new MarquezDatabase(manifestsRoot);
        _ = new OpenMetadataDatabase(manifestsRoot);
    }
    // PostgreSQL CDC Source Connector (Debezium)
    // Migrated from PostgresDebeziumConnector to use the generic builder pattern
    var postgresDebeziumSource = new DebeziumSourceConnectorBuilder(manifestsRoot)
        .WithDatabaseType(DatabaseType.Postgres)
        .WithDatabaseConnection(
            // Kind-local captures the fresh database deployed beside the
            // connector.  Credentials still come from the source Secret, but
            // the host does not, so a legacy address cannot redirect CDC.
            // mac-local keeps the original secret-provided host and port.
            hostname: Constants.IsKindLocal
                ? $"postgres-m3-test.{Constants.KafkaConnectNamespace}.svc.cluster.local"
                : "${env:POSTGRES_HOST}",
            port: Constants.IsKindLocal ? "5432" : "${env:POSTGRES_PORT}",
            user: "${env:POSTGRES_USER}",
            password: "${env:POSTGRES_PASSWORD}",
            database: "${env:POSTGRES_DB}")
        .WithConnectorName("postgres-debezium-source")
        .WithTopicPrefix("m3-cdc")
        .WithPostgresReplication(
            publicationName: Constants.IsKindLocal ? "kind_local_cdc_publication" : "dbz_m3_publication",
            slotName: Constants.IsKindLocal ? "kind_local_cdc_slot" : "m3_debezium_slot",
            pluginName: "pgoutput")
        .WithClusterName(Constants.KafkaConnectClusterName)
        .WithTableIncludeList(Constants.IsKindLocal
            ? new[] { "public.csytab", "public.cidmas", "public.cidven" }
            : new[] { "public.CSYTAB", "public.CIDMAS", "public.CIDVEN" })
        .WithSnapshotMode(SnapshotMode.Always)
        .WithUnwrapTransform(
            enabled: true,
            deleteMode: DeleteHandlingMode.Rewrite,
            addFields: true,
            dropTombstones: true)
        .WithRouteTransform(
            regex: Constants.IsKindLocal
                ? $@"{Constants.IsolationPrefix}\.m3-cdc\.public\.(.*)"
                : "m3-cdc.public.(.*)",
            replacement: Constants.IsKindLocal
                ? $"{Constants.IsolationPrefix}.bronze.m3.$1"
                : "bronze.m3.$1")
        .WithAvroConverter()
        .WithErrorTolerance(tolerateAll: true)
        .WithDeadLetterQueue("m3-debezium-errors")
        .WithPerformanceTuning(
            maxBatchSize: 2048,
            maxQueueSize: 8192,
            pollIntervalMs: 1000)
        .Build();

    // Kafka Connect Cluster
    var kafkaConnectCluster = new KafkaConnectClusterBuilder(manifestsRoot, Constants.KafkaConnectClusterName)
        .WithBootstrapServers(Constants.KafkaBootstrapServers)
        .WithImage("local/kafka-connect:0.47.0-kafka-4.0.0-arm64")
        .WithReplicas(1)
        .WithMetricsConfig("kafka-connect-metrics", "metrics-config.yml")
        .WithResources(
            cpuRequest: Constants.IsKindLocal ? "500m" : "2",
            memoryRequest: Constants.IsKindLocal ? "1Gi" : "4Gi",
            cpuLimit: Constants.IsKindLocal ? "1500m" : "2",
            memoryLimit: Constants.IsKindLocal ? "2Gi" : "8Gi",
            jvmMaxHeap: Constants.IsKindLocal ? "1G" : "5G")
        .Build();

    // ========================================================================
    // BRONZE LAYER CONNECTORS (DD130 Naming)
    // Schema Compatibility: NONE (accept any schema from source systems)
    // ========================================================================
    var icebergSinkCidmas = new IcebergSinkConnectorBuilder(manifestsRoot)
        .WithClusterName(Constants.KafkaConnectClusterName)
        .WithNaming(NamingConventionHelper.DataLayer.Bronze, domain: "m3", dataset: "cidmas")
        .WithIdColumns("idsuno")
        .WithPartitionBy("idcono")
        .Build();

    var icebergSinkCidven = new IcebergSinkConnectorBuilder(manifestsRoot)
        .WithClusterName(Constants.KafkaConnectClusterName)
        .WithNaming(NamingConventionHelper.DataLayer.Bronze, domain: "m3", dataset: "cidven")
        .WithIdColumns("iisuno")
        .WithPartitionBy("iisugr")
        .Build();

    var icebergSinkCsytab = new IcebergSinkConnectorBuilder(manifestsRoot)
        .WithClusterName(Constants.KafkaConnectClusterName)
        .WithNaming(NamingConventionHelper.DataLayer.Bronze, domain: "m3", dataset: "csytab")
        .WithIdColumns("ctstky", "ctstco")
        .WithPartitionBy("ctstco")
        .Build();

    // Debug Connector for Analyst Development
    // Regex-based dynamic routing for kind-local.debug.silver.* topics
    // Fail-fast mode with 10s commits for immediate feedback
    // See: docs/DEVOPS-ANALYST-WAY-OF-WORKING.md
    var debugIcebergSinkSilver = new IcebergSinkConnectorBuilder(manifestsRoot)
        .WithClusterName(Constants.KafkaConnectClusterName)
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
    // var icebergSinkValidExample = new IcebergSinkConnectorBuilder("../manifests")
    //     .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "valid_example")
    //     .WithIdColumns("record_key")
    //     .Build();

    // var icebergSinkCustomerTransactions = new IcebergSinkConnectorBuilder("../manifests")
    //     .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "customer_transactions")
    //     .WithIdColumns("record_key")
    //     .Build();

    // // var icebergSinkProductInventory = new IcebergSinkConnectorBuilder("../manifests")
    // //     .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "product_inventory")
    // //     .WithIdColumns("record_key")
    // //     .Build();

    // // ========================================================================
    // // FLINK JOBS (DD130 Naming)
    // // Auto-derived deployment name: {layer}.{domain}.{dataset}
    // // ========================================================================
    // var scriptFlinkDeployment = new FlinkDeploymentBuilder(manifestsRoot)
    //     .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "valid_example")
    //     .WithSqlS3Uri($"{Constants.S3BucketPath}/schema_validator_test.sql")
    //     .WithUpgradeMode(FlinkDeploymentBuilder.UpgradeMode.LastState)
    //     .Build();

    // var scriptFlinkDeployment2 = new FlinkDeploymentBuilder(manifestsRoot)
    //     .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "customer_transactions")
    //     .WithSqlS3Uri($"{Constants.S3BucketPath}/schema_validator_test_2.sql")
    //     .WithUpgradeMode(FlinkDeploymentBuilder.UpgradeMode.LastState)
    //     .Build();

    var scriptFlinkDeployment3 = new FlinkDeploymentBuilder(manifestsRoot)
        .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "product_inventory")
        .WithSqlS3Uri(Constants.SqlObjectUri("schema_validator_test_3.sql"))
        .WithUpgradeMode(FlinkDeploymentBuilder.UpgradeMode.Stateless)
        .Build();

    // ========================================================================
    // MARQUEZ/OPENLINEAGE LINEAGE DEMO
    //   Job 1+2 (data seed): Run via Flink Session Mode SQL Gateway
    //     - schema_validator_test_debug.sql          -> kind-local.debug.silver.m3.order_summary
    //     - schema_validator_test_debug_customers.sql -> kind-local.debug.silver.m3.customers
    //   Job 3 (streaming transformation): Flink Application Mode deployment
    //     kind-local.debug.silver.m3.order_summary ──────┐
    //     kind-local.debug.silver.m3.customers ──────────┼──► JOIN ──► kind-local.debug.silver.m3.customer_order_analytics
    // ========================================================================
    var debugFlinkLineage = new FlinkDeploymentBuilder(manifestsRoot)
        .WithNaming(NamingConventionHelper.DataLayer.Silver, domain: "m3", dataset: "debug_customer_order_analytics")
        .WithSqlS3Uri(Constants.SqlObjectUri("schema_validator_test_debug_lineage.sql"))
        .WithUpgradeMode(FlinkDeploymentBuilder.UpgradeMode.Stateless)
        .Build();

    var flinkSessionMode = new FlinkClusterBuilder(manifestsRoot)
        .WithTaskSlots(2)
        .WithTaskManagerReplicas(Constants.IsKindLocal ? 1 : 2)
        .WithParallelismDefault(Constants.IsKindLocal ? 1 : 2)
        .WithJobManagerMemory(Constants.IsKindLocal ? "512m" : "1024m")
        .WithTaskManagerMemory(Constants.IsKindLocal ? "768m" : "2048m")
        .Build();
});
