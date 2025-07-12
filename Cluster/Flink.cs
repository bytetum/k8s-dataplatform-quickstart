using Pulumi.Kubernetes.Helm;
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
            Provider = provider,
        });
        // Create a persistent volume for Flink data
        var flinkPv = new PersistentVolume("flink-pv", new PersistentVolumeArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "flink-pv"
            },
            Spec = new PersistentVolumeSpecArgs
            {
                StorageClassName = "standard",
                Capacity = new Dictionary<string, string>
                {
                    { "storage", "10Gi" }
                },
                AccessModes = new[] { "ReadWriteMany" },
                PersistentVolumeReclaimPolicy = "Retain",
                HostPath = new HostPathVolumeSourceArgs
                {
                    Path = "/tmp/flink",
                    Type = "DirectoryOrCreate"
                }
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
        });
        // Create a persistent volume claim for Flink data
        var flinkPvc = new PersistentVolumeClaim("flink-pvc", new PersistentVolumeClaimArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "flink-pvc",
                Namespace = ns.Metadata.Apply(metadata => metadata.Name)
            },
            Spec = new PersistentVolumeClaimSpecArgs
            {
                StorageClassName = "standard",
                AccessModes = new[] { "ReadWriteMany" },
                Resources = new VolumeResourceRequirementsArgs
                {
                    Requests = new Dictionary<string, string>
                    {
                        { "storage", "10Gi" }
                    }
                },
                VolumeName = flinkPv.Metadata.Apply(metadata => metadata.Name)
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
            DependsOn = new[] { flinkPv }
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

        // Create a ConfigMap for logging configuration
        var loggingConfigMap = new ConfigMap("flink-logging-config", new ConfigMapArgs
        {
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "flink-logging-config",
                Namespace = ns.Metadata.Apply(metadata => metadata.Name)
            },
            Data = new InputMap<string>
            {
                { "log4j-console.properties", @"
        # Set root logger level to DEBUG and its only appender to A1.
        rootLogger.level = INFO
        rootLogger.appenderRef.console.ref = ConsoleAppender

        # A1 is set to be a ConsoleAppender.
        appender.console.name = ConsoleAppender
        appender.console.type = CONSOLE
        appender.console.layout.type = PatternLayout
        appender.console.layout.pattern = %d{yyyy-MM-dd HH:mm:ss,SSS} %-5p %-60c %x - %m%n
        " }
            }
        }, new CustomResourceOptions
        {
            Provider = provider
        });
        
        var flinkDeployment = new Kubernetes.ApiExtensions.CustomResource("flink-deployment", new FlinkDeploymentArgs()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "basic-checkpoint-ha-example",
                Namespace = "ns-flink",
            },
            Spec = new Dictionary<string, object>
            {
                ["image"] = "flink:1.20",
                ["flinkVersion"] = "v1_20",
                ["flinkConfiguration"] = new Dictionary<string, object>
                {
                    ["taskmanager.numberOfTaskSlots"] = "2",
                    ["state.savepoints.dir"] = "file:///flink-data/savepoints",
                    ["state.checkpoints.dir"] = "file:///flink-data/checkpoints",
                    ["high-availability"] = "org.apache.flink.kubernetes.highavailability.KubernetesHaServicesFactory",
                    ["high-availability.storageDir"] = "file:///flink-data/ha",
                    ["jobmanager.execution.failover-strategy"] = "region",
                    ["kubernetes.container.image.pull-policy"] = "IfNotPresent",
                    ["jobmanager.archive.fs.dir"] = "file:///flink-data/completed-jobs",
                    ["jobstore.dir"] = "file:///flink-data/job-store",
                    ["jobmanager.scheduler"] = "adaptive",
                    // Add additional debug/logging configuration
                    ["env.java.opts"] = "-verbose:gc -XX:+PrintGCDetails"
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
                ["podTemplate"] = new Dictionary<string, object>
                {
                    ["spec"] = new Dictionary<string, object>
                    {
                        ["initContainers"] = new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                ["name"] = "init-fs",
                                ["image"] = "busybox:1.28",
                                ["command"] = new List<string> { "sh", "-c", "mkdir -p /flink-data/savepoints /flink-data/checkpoints /flink-data/ha /flink-data/completed-jobs /flink-data/job-store && chmod -R 777 /flink-data" },
                                ["volumeMounts"] = new List<Dictionary<string, object>>
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["mountPath"] = "/flink-data",
                                        ["name"] = "flink-volume"
                                    }
                                },
                                ["securityContext"] = new Dictionary<string, object>
                                {
                                    ["runAsUser"] = 0,  // Run as root
                                    ["privileged"] = true
                                }
                            }
                        },
                        ["containers"] = new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                ["name"] = "flink-main-container",
                                ["volumeMounts"] = new List<Dictionary<string, object>>
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["mountPath"] = "/flink-data",
                                        ["name"] = "flink-volume"
                                    }
                                }
                            }
                        },
                        ["volumes"] = new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                ["name"] = "flink-volume",
                                ["persistentVolumeClaim"] = new Dictionary<string, object>
                                {
                                    ["claimName"] = flinkPvc.Metadata.Apply(metadata => metadata.Name)
                                }
                            }
                        },
                        ["securityContext"] = new Dictionary<string, object>
                        {
                            ["fsGroup"] = 9999
                            // Let containers decide their own user/group
                        }
                    }
                },
                ["job"] = new Dictionary<string, object>
                {
                    ["jarURI"] = "local:///opt/flink/examples/streaming/StateMachineExample.jar",
                    ["parallelism"] = 2,
                    ["upgradeMode"] = "last-state",
                    ["state"] = "running"
                }
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
            DependsOn = new List<Pulumi.Resource> { flinkPvc }
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