namespace argocd.applications;

internal class WarpStream
{
    public WarpStream(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("warpstream-agent", provider)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://warpstreamlabs.github.io/charts")
            .Branch("0.15.28")
            .AddValueFile("$values/gitops/manifests/warpstream-agent/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .InNamespace("warpstream-poc")
            .Build();
    }
}