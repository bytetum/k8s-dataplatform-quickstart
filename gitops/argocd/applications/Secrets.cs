
namespace argocd.applications;

internal class Secrets
{
    public Secrets(
        Kubernetes.Provider provider,
        ArgoApplicationSettings settings,
        string destinationNamespace = "secrets")
    {
        new ArgoApplicationBuilder("secrets", provider, settings)
            .SyncWave(1)
            .InNamespace(destinationNamespace)
            .Build();
    }
}
