namespace argocd.applications;

internal class WarpStreamSchemaRegistry
{
    public WarpStreamSchemaRegistry(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("warpstream-schema-registry", provider)
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://warpstreamlabs.github.io/charts")
            .Branch("0.15.28")
            .Chart("warpstream-agent")
            .AddValueFile("$values/gitops/manifests/warpstream-schema-registry/values.yaml")
            .InNamespace("warpstream")
            .Build();
    }
}