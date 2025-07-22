
namespace argocd.applications;

internal class Secrets
{
    public Secrets(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("secrets", provider)
            .SyncWave(1)
            .Build();
    }
}
