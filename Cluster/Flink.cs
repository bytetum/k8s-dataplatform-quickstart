using Pulumi.Kubernetes.Helm;
using Kubernetes = Pulumi.Kubernetes;
namespace infrastructure.Cluster;
using Pulumi;
using Kubernetes.Core.V1;
using Kubernetes.Types.Inputs.Meta.V1;
using Kubernetes.Types.Inputs.Core.V1;
using System.Collections.Generic;

public class Flink : ComponentResource
{
    public Flink(CertManager certManager,Kubernetes.Provider? provider = null) : base("flink-installation", "flink-installation")
    {

        var ns = new Namespace("ns-flink", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "ns-flink"
            }
        }, new CustomResourceOptions
        {
            Provider = provider
        });
        
        var flinkOperator = new Pulumi.Kubernetes.Helm.V3.Chart("flink-operator", new ChartArgs()
        {
            Namespace = ns.Metadata.Apply(metadata => metadata.Name),
            Chart = "flink-kubernetes-operator",
            Version = "1.12.1",
            FetchOptions = new ChartFetchArgs()
            {
                Repo = "https://downloads.apache.org/flink/flink-kubernetes-operator-1.12.1/",
            },
        }, new ComponentResourceOptions
        {
            DependsOn = new List<Pulumi.Resource> { certManager },
            Provider = provider
        });
        
        var flinkDeployment = new Kubernetes.ApiExtensions.CustomResource("flink-deployment", new FlinkDeploymentArgs()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "basic-example",
                Namespace = "ns-flink",
            },
            Spec = new Dictionary<string, object>
            {
                ["image"] = "flink:1.16",
                ["flinkVersion"] = "v1_16",
                ["flinkConfiguration"] = new Dictionary<string, object>
                {
                    ["taskmanager.numberOfTaskSlots"] = "2"
                },
                ["serviceAccount"] = "flink",
                ["jobManager"] = new Dictionary<string, object>
                {
                    ["resource"] = new Dictionary<string, object>
                    {
                        ["memory"] = "2048m",
                        ["cpu"] = 1
                    }
                },
                ["taskManager"] = new Dictionary<string, object>
                {
                    ["resource"] = new Dictionary<string, object>
                    {
                        ["memory"] = "2048m",
                        ["cpu"] = 1
                    }
                },
                ["job"] = new Dictionary<string, object>
                {
                    ["jarURI"] = "local:///opt/flink/examples/streaming/StateMachineExample.jar",
                    ["parallelism"] = 2,
                    ["upgradeMode"] = "stateless",
                    ["state"] = "running"
                }
            }
        }, new CustomResourceOptions
        {
            Provider = provider
        });
        
    }

    internal class FlinkDeploymentArgs : Kubernetes.ApiExtensions.CustomResourceArgs
    {
        public FlinkDeploymentArgs() : base("flink.apache.org/v1beta1", "FlinkDeployment")
        {
            
        }
        [Input("spec")]
        public Dictionary<string, object>? Spec { get; set; }
    }
}