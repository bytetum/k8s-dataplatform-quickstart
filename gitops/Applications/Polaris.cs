namespace argocd.applications;

public class Polaris
{
    public Polaris(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("polaris", provider)
            .Type(ApplicationType.Helm)
            .SyncWave(2)
            .RepoUrl("https://downloads.apache.org/incubator/polaris/helm-chart/")
            .Build();
    }
}