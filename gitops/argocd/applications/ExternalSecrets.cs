namespace argocd.applications;

internal class ExternalSecrets
{
    public ExternalSecrets(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        var externalSecretsInstallation = new ArgoApplicationBuilder("external-secrets", provider, settings)
            .AddSource(ApplicationType.Helm)
            .Branch("0.18.2")
            .RepoUrl("https://charts.external-secrets.io")
            .SyncWave(0)
            .Build();
        
        var secrets = new ArgoApplicationBuilder("secrets", provider, settings)
            .SyncWave(1)
            .Build();
    }
}
