namespace argocd.applications;

internal class ExternalSecrets
{
    public ExternalSecrets(Kubernetes.Provider provider)
    {
        var externalSecretsInstallation = new ArgoApplicationBuilder("external-secrets", provider)
            .Type(ApplicationType.Helm)
            .Branch("0.18.2")
            .RepoUrl("https://charts.external-secrets.io")
            .SyncWave(0)
            .Build();
        
        var secrets = new ArgoApplicationBuilder("secrets", provider)
            .SyncWave(1)
            .Build();
    }
}
