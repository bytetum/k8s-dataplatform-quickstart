namespace argocd.applications;

public class Trino
{
    public Trino(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("trino", provider)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://trinodb.github.io/charts")
            .Branch("0.27.0")  // Latest stable version of Trino Helm chart
            .AddValueFile("$values/gitops/manifests/trino/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .InNamespace("trino")
            .SyncWave(3)  // Deploy after Polaris (wave 2)
            .Build();
    }
}