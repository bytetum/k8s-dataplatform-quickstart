namespace argocd.applications;

public class Polaris
{
    public Polaris(Pulumi.Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("polaris", provider, settings)
            .AddSource(ApplicationType.Helm)
            .Branch("1.3.0-incubating")
            .RepoUrl("https://downloads.apache.org/incubator/polaris/helm-chart")
            .AddValueFile("$values/gitops/manifests/polaris/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(2)
            .Build();
    }
}
