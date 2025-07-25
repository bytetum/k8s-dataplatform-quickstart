namespace argocd.applications;

public class Polaris
{
    public Polaris(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("polaris", provider)
            .AddSource(ApplicationType.Helm)
            .SyncWave(2)
            .Branch("1.0.0-incubating")
            .RepoUrl("https://downloads.apache.org/incubator/polaris/helm-chart")
            .Build();
    }
}