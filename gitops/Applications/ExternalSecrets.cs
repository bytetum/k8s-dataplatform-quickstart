namespace argocd.applications;

internal class ExternalSecrets
{
    public ExternalSecrets(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("external-secrets", provider)
            .Type(ApplicationType.Helm)
            .SyncWave(0)
            .TargetRevision("0.18.2")
            .HelmValues("https://charts.external-secrets.io")
            .Build();
    }
}
