global using Pulumi;
global using Kubernetes = Pulumi.Kubernetes;
using applications.flink.flink_deployment;
using applications.infrastructure;
using applications.warpstream;
using applications.polaris;
using applications.postgres;
using applications.kafkaconnect;

return await Deployment.RunAsync(() =>
{
    var infrastructure = new Infrastructure("../manifests");
    var flinkDeployment = new FlinkDeployment("flink-deployment", "../manifests");
    var warpstream = new Warpstream("../manifests");
    var WarpstreamSchemaRegistry = new WarpstreamSchemaRegistry("../manifests");
    var polaris = new Polaris("../manifests");
    var postgres = new Postgres("../manifests");
    var kafkaConnectCluster = new KafkaConnect("../manifests");
    var postgreDebeziumConnector = new PostgresDebeziumConnector("../manifests");
    var polarisSinkConncetor = new PolarisSinkConnector("../manifests");
});