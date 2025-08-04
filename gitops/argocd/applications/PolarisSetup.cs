namespace argocd.applications;

public class PolarisSetup
{
    public PolarisSetup(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("polaris-setup", provider)
            .SyncWave(10)
            .Build();
    }
}