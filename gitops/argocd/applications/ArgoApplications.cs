using argocd.applications.flink_deployment;

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
        var flinkOperator = new FlinkOperator(provider);
        var flinkDeployment = new FlinkDeployment(provider);
        var polaris = new Polaris(provider);
    }
}

