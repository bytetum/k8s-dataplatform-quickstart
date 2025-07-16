namespace argocd.applications;

internal class ExternalSecrets
{
    public ExternalSecrets(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("external-secrets", provider)
            .Type(ApplicationType.Chart)
            .SyncWave(0)
            .HelmValues("https://charts.external-secrets.io")
            .Build();
    }
}
