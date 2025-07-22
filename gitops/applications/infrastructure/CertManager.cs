namespace applications.infrastructure;

internal class CertManager : ComponentResource
{
    public CertManager(string manifestsRoot)
        : base("cert-manager", "cert-manager")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/cert-manager"
        }, new CustomResourceOptions
        {
            Parent = this
        });

        var certManager = new Kubernetes.Yaml.ConfigFile("cert-manager", new()
        {
            File = "https://github.com/cert-manager/cert-manager/releases/download/v1.18.2/cert-manager.yaml",
        }, new()
        {
            Provider = provider,
            Parent = this
        });
    }
}