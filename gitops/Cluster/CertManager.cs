namespace gitops.Cluster;

using Pulumi;
using Kubernetes = Pulumi.Kubernetes;

public class CertManager : ComponentResource
{
    public CertManager(Kubernetes.Provider? provider = null) : base("cert-manager-installation",
        "cert-manager-installation")
    {
        var certManager = new Kubernetes.Yaml.ConfigFile("cert-manager", new()
        {
            File = "https://github.com/cert-manager/cert-manager/releases/download/v1.18.2/cert-manager.yaml",
        }, new()
        {
            Provider = provider,
        });
    }
}