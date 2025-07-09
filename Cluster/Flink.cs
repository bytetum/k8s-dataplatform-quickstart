using Pulumi;
using Pulumi.Kubernetes.ApiExtensions;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using System.Collections.Generic;
using Pulumi.Kubernetes;
using Pulumi.Kubernetes.ApiExtensions.V1;
using Pulumi.Kubernetes.Types.Inputs.ApiExtensions.V1;
using Pulumi.Kubernetes.Yaml;
using Kubernetes = Pulumi.Kubernetes;

namespace infrastructure.Cluster;
using Pulumi;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Helm.V4;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using System.Collections.Generic;
using System.IO;
public class Flink : ComponentResource
{
    public Flink(CertManager certManager) : base("flink-installation", "flink-installation")
    {
        var config = new Config();

        var ns = new Namespace("ns-flink", new()
        {
            Metadata = new Pulumi.Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "ns-flink"
            }
        });


        var flinkOperator = new Pulumi.Kubernetes.Helm.V4.Chart("flink-operator", new()
        {
            Namespace = ns.Metadata.Apply(metadata => metadata.Name),
            Chart = "flink-kubernetes-operator",
            Version = "1.12.0",
            RepositoryOpts = new Pulumi.Kubernetes.Types.Inputs.Helm.V4.RepositoryOptsArgs
            {
                Repo = "https://downloads.apache.org/flink/flink-kubernetes-operator-1.12.0/",
            },
        }, new()
        {
            DependsOn = certManager
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
        }, new CustomResourceOptions());
        
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