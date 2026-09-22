namespace argocd.applications;

public class Trino
{
    public Trino(Pulumi.Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("trino", provider, settings)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://trinodb.github.io/charts")
            .Branch("1.41.0")  // Latest stable version of Trino Helm chart
            .AddValueFile($"$values/{settings.ManifestRoot}/trino/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .InNamespace(settings.WorkloadNamespace("trino", "lakehouse-trino"))
            .SyncWave(3)  // Deploy after Polaris (wave 2)
            .Build();
    }
}
