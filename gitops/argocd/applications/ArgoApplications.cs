using argocd.applications.flink_deployment;
using argocd.applications.kafka_connect;

namespace argocd.applications;

internal class ArgoApplications : ComponentResource
{
    public ArgoApplications(string manifestsRoot)
        : base("manifests", "argo-applications")
    {
        var provider = new Kubernetes.Provider("argocd-application-provider", new()
        {
            RenderYamlToDirectory = manifestsRoot,
        });

        var certManager = new CertManager(provider);
        var externalSecrets = new ExternalSecrets(provider);
		var warpStream = new WarpStream(provider);
        var flinkOperator = new FlinkOperator(provider);
        var flinkDeployment = new FlinkDeployment(provider);
        var polaris = new Polaris(provider);
        // var trino = new Trino(provider);
        var warpStreamSchemaRegistry = new WarpStreamSchemaRegistry(provider);
        var strimziOperator = new StrimziOperator(provider);
        var kafkaConnect = new KafkaConnect(provider);
        // var monitoring = new KubePrometheus(provider);
    }
}

