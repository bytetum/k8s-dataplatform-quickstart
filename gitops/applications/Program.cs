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
    var kafkaConnectCluster = new KafkaConnectClusterBuilder("m3-kafka-connect")
        .WithBootstrapServers("warpstream-agent.warpstream.svc.cluster.local:9092")
        .WithImage("ttl.sh/hxt-kafka-connect-amd64-20-12:24h")
        .WithReplicas(1)
        .WithMetricsConfig("kafka-connect-metrics", "metrics-config.yml")
        .WithResources(
            cpuRequest: "2",
            memoryRequest: "4Gi",
            cpuLimit: "2",
            memoryLimit: "6Gi",
            jvmMaxHeap: "4G")
        .Build("../manifests");

    // Iceberg Sink Connectors
    var icebergSinkCidmas = new IcebergSinkConnectorBuilder("../manifests")
        .WithConnectorName("cidmas")
        .WithSourceTopic("bronze.m3.cidmas")
        .WithDestinationTable("m3_bronze.cidmas")
        .WithIdColumns("idcono", "idsuno")
        .WithPartitionBy("idcono")
        .Build();

    var icebergSinkCidven = new IcebergSinkConnectorBuilder("../manifests")
        .WithConnectorName("cidven")
        .WithSourceTopic("bronze.m3.cidven")
        .WithDestinationTable("m3_bronze.cidven")
        .WithIdColumns("iisuno")
        .WithPartitionBy("iisugr")
        .Build();

    var icebergSinkCsytab = new IcebergSinkConnectorBuilder("../manifests")
        .WithConnectorName("csytab")
        .WithSourceTopic("bronze.m3.csytab")
        .WithDestinationTable("m3_bronze.csytab")
        .WithIdColumns("ctstco", "ctstky")
        .WithPartitionBy("ctstco")
        .Build();

    var jarFlinkDeployment = new FlinkDeploymentBuilder("../manifests")
        .WithDeploymentName("sql-runner-exampl-3")
        .WithJarS3Uri("s3://local-rocksdb-test/examples-scala.jar")
        .WithEntryClass("io.github.streamingwithflink.chapter1.AverageSensorReadings")
        .WithUpgradeMode(FlinkDeploymentBuilder.UpgradeMode.Stateless)
        .Build();
    
    var scriptFlinkDeployment = new FlinkDeploymentBuilder("../manifests")
        .WithDeploymentName("sql-runner-example-script")
        .WithSqlS3Uri("s3://local-rocksdb-test/flink-sql-runner-script.sql")
        .WithUpgradeMode(FlinkDeploymentBuilder.UpgradeMode.Stateless)
        .Build();

    var flinkSessionMode = new FlinkClusterBuilder("../manifests")
        .WithTaskSlots(2)
        .WithTaskManagerReplicas(2)
        .WithParallelismDefault(2)
        .WithJobManagerMemory("1024m")
        .WithTaskManagerMemory("2048m")
        .Build();
});