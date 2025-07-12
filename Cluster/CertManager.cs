using Pulumi.Kubernetes.Types.Inputs.Yaml.V2;
using Pulumi.Kubernetes.Yaml.V2;

namespace infrastructure.Cluster;
using Pulumi;
using Kubernetes = Pulumi.Kubernetes;
using Pulumi.Kubernetes.Core.V1;
using System.Collections.Generic;
public class CertManager : ComponentResource
{
    public CertManager(Kubernetes.Provider? provider = null) : base("cert-manager-installation", "cert-manager-installation")
    {

        var certManagerCrds = new ConfigFile("cert-manager-crds", new ConfigFileArgs
        {
            File = "https://github.com/cert-manager/cert-manager/releases/download/v1.18.2/cert-manager.crds.yaml"
        }, new ComponentResourceOptions
        {
            Provider = provider
        });

        var ns = new Namespace("ns-cert-manager", new()
        {
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "ns-cert-manager"
            }
        }, new CustomResourceOptions
        {
            Provider = provider
        });
        
        var certManager = new Kubernetes.Helm.V4.Chart("cert-manager", new()
        {
            Namespace = ns.Metadata.Apply(metadata => metadata.Name),
            Chart = "cert-manager",
            Version = "1.18.2",
            RepositoryOpts = new Kubernetes.Types.Inputs.Helm.V4.RepositoryOptsArgs
            {
                Repo = "https://charts.jetstack.io",
            }
        },  new ComponentResourceOptions
        {
            DependsOn = new List<Pulumi.Resource> { certManagerCrds, ns },
            Provider = provider
        });
    }
}