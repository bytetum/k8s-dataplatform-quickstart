namespace argocd.applications;

public class Polaris
{
    public Polaris(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("polaris", provider)
            .AddSource(ApplicationType.Helm)
            .Branch("1.0.0-incubating")
            .RepoUrl("https://downloads.apache.org/incubator/polaris/helm-chart")
            .AddValueFile("gitops/manifests/polaris/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .SyncWave(2)
            .Build();
    }
}