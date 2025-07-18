namespace argocd.applications;

internal class WarpStream
{
    public WarpStream(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("warpstream-agent", provider)
            .Type(ApplicationType.Helm)
            .SyncWave(2)
            .RepoUrl("https://warpstreamlabs.github.io/charts")
            .InNamespace("warpstream-poc")
            .Build();
    }
}