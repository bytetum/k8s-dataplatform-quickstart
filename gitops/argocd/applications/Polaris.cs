namespace argocd.applications;

public class Polaris
{
    public Polaris(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("polaris", provider)
            .AddSource(ApplicationType.Helm)
            .AddValueFile("$values/gitops/manifests/polaris/default-values.yaml")
            .SyncWave(2)
            .Branch("1.0.0-incubating")
            .RepoUrl("https://downloads.apache.org/incubator/polaris/helm-chart")
            .Build();
    }
}