using Pulumi.Kubernetes.Helm;
using Pulumi.Kubernetes.Types.Inputs.Helm.V4;
using Kubernetes = Pulumi.Kubernetes;

namespace infrastructure.Cluster;

using Pulumi;
using Kubernetes.Core.V1;
using Kubernetes.Types.Inputs.Meta.V1;
using Kubernetes.Types.Inputs.Core.V1;
using System.Collections.Generic;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Storage.V1;

public class Flink : ComponentResource
{
    public Flink(CertManager certManager, Kubernetes.Provider? provider = null) : base("flink-installation",
        "flink-installation")
    {
        var ns = new Namespace("ns-flink", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = Constants.Namespace
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
        });


        var flinkOperator = new Pulumi.Kubernetes.Helm.V4.Chart("flink-operator", new ()
        {
            Namespace = ns.Metadata.Apply(metadata => metadata.Name),
            Chart = "flink-kubernetes-operator",
            Version = "1.12.1",
            RepositoryOpts = new RepositoryOptsArgs()
            {
                Repo = "https://downloads.apache.org/flink/flink-kubernetes-operator-1.12.1/",
            },
        }, new ComponentResourceOptions
        {
            DependsOn = new List<Pulumi.Resource> { certManager },
            Provider = provider
        });
    }
}