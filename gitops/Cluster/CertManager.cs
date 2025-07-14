using Pulumi.Kubernetes.Helm;
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
        
        var certManager = new Kubernetes.Helm.V3.Chart("cert-manager", new ChartArgs()
        {
            Namespace = ns.Metadata.Apply(metadata => metadata.Name),
            Chart = "cert-manager",
            Version = "1.18.2",
            FetchOptions = new ChartFetchArgs()
            {
                Repo = "https://charts.jetstack.io",
            },
            Values = new Dictionary<string, object>
            {
                ["installCRDs"] = true
            }
        },  new ComponentResourceOptions
        {
            DependsOn = new List<Pulumi.Resource> { ns },
            Provider = provider
        });
    }
}