namespace argocd.applications;

internal class CertManager
{
    public CertManager(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("cert-manager", provider, settings)
            .SyncWave(-1)
            .Build();
    }
}
