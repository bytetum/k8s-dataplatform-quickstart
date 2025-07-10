using infrastructure.Cluster;
using Pulumi;
using Kubernetes = Pulumi.Kubernetes;

namespace infrastructure;

internal class Infrastructure : Stack
{
    public Infrastructure()
        : base()
    {
        var k8sProvider = new Kubernetes.Provider("k8s-provider", new()
        {
            // Use the current kubectl context (default)
        });

        var certManager = new CertManager(k8sProvider);
        var flink = new Flink(certManager, k8sProvider);
    }
}

