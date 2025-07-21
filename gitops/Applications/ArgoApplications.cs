using argocd.applications.flink_deployment;
using Pulumi;

namespace argocd.applications;

internal class ArgoApplications : ComponentResource
{
    public ArgoApplications(string manifestsRoot)
        : base("manifests", "argo-applications")
    {
        var provider = new Pulumi.Kubernetes.Provider("argocd-application-provider", new()
        {
            RenderYamlToDirectory = manifestsRoot,
        });

        var certManager = new CertManager(provider);
        var externalSecrets = new ExternalSecrets(provider);
        var flinkOperator = new FlinkOperator(provider);
        var flinkDeployment = new FlinkDeployment(provider);
        var warpStreamAgent = new WarpStream(provider);
        var polaris = new Polaris(provider);
    }
}

