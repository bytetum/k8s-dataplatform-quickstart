namespace argocd.applications;

internal class WarpStreamSchemaRegistry
{
    public WarpStreamSchemaRegistry(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("warpstream-schema-registry", provider, settings)
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://warpstreamlabs.github.io/charts")
            .Branch("1.0.5")
            .Chart("warpstream-agent")
            .AddValueFile($"$values/{settings.ManifestRoot}/warpstream-schema-registry/values.yaml")
            .InNamespace(settings.WorkloadNamespace("warpstream", "lakehouse-warpstream"))
            .Build();
    }
}
