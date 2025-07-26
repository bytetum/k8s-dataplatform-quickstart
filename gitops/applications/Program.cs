global using Pulumi;
global using Kubernetes = Pulumi.Kubernetes;
using applications.flink.flink_deployment;
using applications.infrastructure;
using applications.Polaris.external_secrets;

return await Deployment.RunAsync(() =>
{
    var infrastructure = new Infrastructure("../manifests");
    var flinkDeployment = new FlinkDeployment("flink-deployment", "../manifests");
    var bucketSecret = new BucketSecret("../manifests/polaris");
});