namespace argocd.applications;

public class Polaris
{
    public Polaris(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("polaris", provider)
            .AddSource(ApplicationType.Helm)
            .Branch("1.2.0")
            .RepoUrl("https://downloads.apache.org/incubator/polaris/helm-chart")
            .AddValueFile("$values/gitops/manifests/polaris/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(2)
            .Build();
    }
}