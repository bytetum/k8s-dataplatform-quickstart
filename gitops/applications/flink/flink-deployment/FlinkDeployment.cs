using System.Collections.Generic;
using System.IO;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.flink.flink_deployment;

internal class FlinkDeployment : ComponentResource
{
    public FlinkDeployment(string deploymentName, string manifestsRoot) : base(
        "flink-deployment",
        "flink-deployment")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/{deploymentName}"
        }, new CustomResourceOptions
        {
            Parent = this
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
            Parent = this
        });
        // Create a persistent volume claim for Flink data
        var flinkPvc = new PersistentVolumeClaim("flink-pvc", new PersistentVolumeClaimArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "flink-pvc",
                Namespace = Constants.Namespace
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
            DependsOn = new[] { flinkPv },
            Parent = this
        });

        var sqlFileContent = File.ReadAllText("./flink/flink-deployment/test_job.sql");
        var sqlScriptConfigMap = new ConfigMap("flink-sql-script-cm", new ConfigMapArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "flink-sql-script",
                Namespace = Constants.Namespace,
            },
            Data =
            {
                { "test_job.sql", sqlFileContent }
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = this
        });

        var flinkKafkaCredentialsSecret = new ExternalSecret("flink-warpstream-credentials-secret", new ExternalSecretArgs()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "flink-warpstream-credentials-secret",
                Namespace = Constants.Namespace
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = "secret-store",
                    Kind = "ClusterSecretStore"
                },
                Target = new ExternalSecretSpecTargetArgs()
                {
                    Name = "flink-warpstream-credentials-secret"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = "id:flink-warpstream-credentials-secret"
                    }
                }    
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = this
        });

        var flinkDeploymentSql = new Kubernetes.ApiExtensions.CustomResource("flink-deployment-sql",
            new FlinkDeploymentArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "basic-checkpoint-ha-sql-example",
                    Namespace = Constants.Namespace,
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
                        ["high-availability"] =
                            "org.apache.flink.kubernetes.highavailability.KubernetesHaServicesFactory",
                        ["high-availability.storageDir"] = "file:///flink-data/ha",
                        ["jobmanager.archive.fs.dir"] = "file:///flink-data/completed-jobs",
                        ["jobstore.dir"] = "file:///flink-data/job-store",
                        ["jobmanager.scheduler"] = "adaptive",
                        // Add additional debug/logging configuration
                        ["env.java.opts"] = "-verbose:gc -XX:+PrintGCDetails",
                        // Kafka configuration
                        ["kafka.bootstrap.servers"] = "warpstream-agent.default.svc.cluster.local:9092",
                        ["kafka.input.topic"] = "input-topic",
                        ["kafka.output.topic"] = "output-topic",
                        // IMPORTANT: Define your credentials here, not in the SQL script
                        //              ["kafka.sasl.jaas.config"] = org.apache.kafka.common.security.plain.PlainLoginModule required username=\"${env.USERNAME}\" password=\"${env.PASSWORD}\";,
                    },
                    ["serviceAccount"] = "flink",
                    ["jobManager"] = new Dictionary<string, object>
                    {
                        ["resource"] = new Dictionary<string, object>
                        {
                            ["memory"] = "2048m",
                            ["cpu"] = 1,
                        },
                        ["podTemplate"] = new Dictionary<string, object>
                        {
                            ["spec"] = new Dictionary<string, object>
                            {
                                ["containers"] = new[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["name"] = "flink-main-container",
                                        ["envFrom"] = new[]
                                        {
                                            new Dictionary<string, object>
                                            {
                                                ["secretRef"] = new Dictionary<string, object>
                                                {
                                                    ["name"] = "flink-warpstream-credentials-secret"
                                                }
                                            }
                                        },
                                        ["volumeMounts"] = new[]
                                        {
                                            new Dictionary<string, object>
                                            {
                                                ["mountPath"] = "/opt/flink/sql/job.sql",
                                                ["name"] = "flink-sql-script-volume",
                                                ["subPath"] = "job.sql"
                                            }
                                        }
                                    }
                                },
                                ["volumes"] = new[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["name"] = "flink-sql-script-volume",
                                        ["configMap"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "flink-sql-script"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    ["service"] = new Dictionary<string, object>
                    {
                        ["type"] = "ClusterIP",
                        ["port"] = 8081,
                        ["targetPort"] = 8081
                    },
                    ["taskManager"] = new Dictionary<string, object>
                    {
                        ["resource"] = new Dictionary<string, object>
                        {
                            ["memory"] = "2048m",
                            ["cpu"] = 1
                        }
                    },
                    // Add the job configuration
                    ["job"] = new Dictionary<string, object>
                    {
                        ["jarURI"] = "local:///opt/flink/opt/flink-sql-client-1.20.2.jar",
                        ["args"] = new[]
                        {
                            "-f",
                            "/opt/flink/sql/job.sql"
                        },
                        ["parallelism"] = 1,
                        ["upgradeMode"] = "stateless"
                    }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });
    }

    private class FlinkDeploymentArgs : Kubernetes.ApiExtensions.CustomResourceArgs
    {
        public FlinkDeploymentArgs() : base("flink.apache.org/v1beta1", "FlinkDeployment")
        {
        }

        [Input("spec")] public Dictionary<string, object>? Spec { get; set; }
    }
}

