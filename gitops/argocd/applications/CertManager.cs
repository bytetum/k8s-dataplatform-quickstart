namespace argocd.applications;

internal class CertManager
{
    public CertManager(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("cert-manager", provider)
            .SyncWave(-1)
            .Build();
    }
}
