namespace argocd.applications;

internal class CertManager
{
    public CertManager(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("cert-manager", provider)
            .Build();
    }
}
