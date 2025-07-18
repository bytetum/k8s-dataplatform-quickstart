namespace argocd.applications;

internal class WarpStream
{
    public WarpStream(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("warpstream-agent", provider)
            .Type(ApplicationType.Helm)
            .RepoUrl("https://warpstreamlabs.github.io/charts")
            .Branch("0.15.28")
            .Build();
    }
}