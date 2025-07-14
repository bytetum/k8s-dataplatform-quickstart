using infrastructure.Cluster;
using Pulumi;
using Kubernetes = Pulumi.Kubernetes;

namespace infrastructure;

internal class Infrastructure : Stack
{
    public Infrastructure()
        : base()
    {
        
        var k8sProviderCert = new Kubernetes.Provider("k8s-provider-cert", new()
        {
            RenderYamlToDirectory = "manifests/cert-manager",
            EnableServerSideApply = false,
        });
        
        var k8sProviderFlink = new Kubernetes.Provider("k8s-provider-flink", new()
        {
            RenderYamlToDirectory = "manifests/flink",
            EnableServerSideApply = false,
            
            // Use the current kubectl context (default)
        });
        
        var k8sProviderDeployment = new Kubernetes.Provider("k8s-provider-flink-deployment", new()
        {
            RenderYamlToDirectory = "manifests/flink-deployment",
            EnableServerSideApply = false,
            // Use the current kubectl context (default)
        });

        var certManager = new CertManager(k8sProviderCert);
        var flink = new Flink(certManager, k8sProviderFlink);
        var flinkDeployment = new FlinkDeployment(flink, k8sProviderDeployment);
    }
}

