
namespace argocd.applications;

internal class Secrets
{
    public Secrets(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("secrets", provider, settings)
            .SyncWave(1)
            .Build();
    }
}
