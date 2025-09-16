using System.Collections.Generic;
using System.IO;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Crds.Flink;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using ServiceSpecArgs = Pulumi.Crds.Flink.ServiceSpecArgs;

namespace applications.flink.flink_deployment
{
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
                    { "job.sql", sqlFileContent }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });

            var hiveSiteContent = File.ReadAllText("./flink/flink-deployment/hive-site.xml");
            var hiveConfigMap = new ConfigMap("hive-conf-cm", new ConfigMapArgs
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "hive-conf",
                    Namespace = Constants.Namespace,
                },
                Data =
                {
                    { "hive-site.xml", hiveSiteContent }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });

            var flinkConfContent = File.ReadAllText("./flink/flink-deployment/flink-conf.yaml");
            var flinkConfConfigMap = new ConfigMap("flink-conf-cm", new ConfigMapArgs
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "flink-conf",
                    Namespace = Constants.Namespace,
                },
                Data =
                {
                    { "config.yaml", flinkConfContent }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });

            var flinkKafkaCredentialsSecret = new ExternalSecret("flink-warpstream-credentials-secret",
                new ExternalSecretArgs
                {
                    Metadata = new ObjectMetaArgs
                    {
                        Name = "flink-warpstream-credentials-secret",
                        Namespace = Constants.Namespace
                    },
                    Spec = new ExternalSecretSpecArgs
                    {
                        SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs
                        {
                            Name = "secret-store",
                            Kind = "ClusterSecretStore"
                        },
                        Target = new ExternalSecretSpecTargetArgs
                        {
                            Name = "flink-warpstream-credentials-secret"
                        },
                        DataFrom = new InputList<ExternalSecretSpecDataFromArgs>
                        {
                            new ExternalSecretSpecDataFromArgs
                            {
                                Extract = new ExternalSecretSpecDataFromExtractArgs
                                {
                                    Key = "id:flink-warpstream-credentials-secret"
                                }
                            }
                        }
                    }
                }, new CustomResourceOptions
                {
                    Provider = provider,
                    Parent = this
                });

            var flinkDeployment = new Pulumi.Crds.Flink.FlinkDeployment("flink-deployment", new FlinkDeploymentArgs
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "basic-checkpoint-ha-sql-example",
                    Namespace = Constants.Namespace
                },
                Spec = new FlinkDeploymentSpecArgs
                {
                    Image = "flink:latest",
                    FlinkVersion = "v1_20",
                    FlinkConfiguration = new FlinkConfigurationSpecArgs
                    {
                        TaskManagerNumberOfTaskSlots = "2",
                        StateSavepointsDir = "file:///flink-data/savepoints",
                        StateCheckpointsDir = "file:///flink-data/checkpoints",
                        HighAvailability = "org.apache.flink.kubernetes.highavailability.KubernetesHaServicesFactory",
                        HighAvailabilityStorageDir = "file:///flink-data/ha",
                        JobManagerArchiveFsDir = "file:///flink-data/completed-jobs",
                        JobStoreDir = "file:///flink-data/job-store",
                        JobManagerScheduler = "adaptive",
                        EnvJavaOpts = "-verbose:gc -XX:+PrintGCDetails",
                        KafkaBootstrapServers = "warpstream-agent.default.svc.cluster.local:9092",
                        KafkaInputTopic = "input-topic",
                        KafkaOutputTopic = "output-topic",
                    },
                    ServiceAccount = "flink",
                    JobManager = new JobManagerSpecArgs
                    {
                        Resource = new ResourceSpecArgs
                        {
                            Memory = "2048m",
                            Cpu = 1
                        },
                    },
                    TaskManager = new TaskManagerSpecArgs
                    {
                        ServiceAccount = "flink",
                        Resource = new ResourceSpecArgs
                        {
                            Memory = "2048m",
                            Cpu = 1
                        }
                    },
                    PodTemplate = new Pulumi.Crds.Flink.PodTemplateSpecArgs
                    {
                        ServiceAccount = "flink",
                        Spec = new PodSpecArgs
                        {
                            InitContainers = new List<ContainerArgs>
                            {
                                new ContainerArgs
                                {
                                    Name = "init-fs",
                                    Image = "busybox:1.28",
                                    Command = new List<string>
                                    {
                                        "sh", "-c",
                                        "mkdir -p /opt/flink/lib /opt/flink/sql /flink-data/savepoints /flink-data/checkpoints /flink-data/ha /flink-data/completed-jobs /flink-data/job-store && chmod -R 777 /flink-data /opt/flink && wget -O /opt/flink/lib/flink-connector-hive_2.12-1.20.2.jar https://repo.maven.apache.org/maven2/org/apache/flink/flink-connector-hive_2.12/1.20.2/flink-connector-hive_2.12-1.20.2.jar && wget -O /opt/flink/lib/hive-exec-2.3.4.jar https://repo.maven.apache.org/maven2/org/apache/hive/hive-exec/2.3.4/hive-exec-2.3.4.jar && wget -O /opt/flink/lib/antlr-runtime-3.5.2.jar https://repo.maven.apache.org/maven2/org/antlr/antlr-runtime/3.5.2/antlr-runtime-3.5.2.jar"
                                    },
                                    VolumeMounts = new List<VolumeMountArgs>
                                    {
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/flink-data",
                                            Name = "flink-volume"
                                        }
                                    },
                                    SecurityContext = new SecurityContextArgs
                                    {
                                        RunAsUser = 0,
                                        Privileged = true
                                    }
                                }
                            },
                            Containers = new List<ContainerArgs>
                            {
                                new ContainerArgs
                                {
                                    Name = "flink-main-container",
                                    EnvFrom = new List<EnvFromSourceArgs>
                                    {
                                        new EnvFromSourceArgs
                                        {
                                            SecretRef = new SecretEnvSourceArgs
                                            {
                                                Name = "flink-warpstream-credentials-secret"
                                            }
                                        }
                                    },
                                    VolumeMounts = new List<VolumeMountArgs>
                                    {
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/flink-data",
                                            Name = "flink-volume"
                                        },
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/opt/flink/sql/job.sql",
                                            Name = "flink-sql-script-volume",
                                            SubPath = "job.sql"
                                        },
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/opt/flink/hive-conf",
                                            Name = "hive-conf-volume"
                                        },
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/opt/flink/conf",
                                            Name = "flink-conf-volume"
                                        }
                                    }
                                }
                            },
                            Volumes = new List<VolumeArgs>
                            {
                                new VolumeArgs
                                {
                                    Name = "flink-volume",
                                    PersistentVolumeClaim = new PersistentVolumeClaimVolumeSourceArgs
                                    {
                                        ClaimName = flinkPvc.Metadata.Apply(m => m.Name)
                                    }
                                },
                                new VolumeArgs
                                {
                                    Name = "flink-sql-script-volume",
                                    ConfigMap = new ConfigMapVolumeSourceArgs
                                    {
                                        Name = sqlScriptConfigMap.Metadata.Apply(m => m.Name)
                                    }
                                },
                                new VolumeArgs
                                {
                                    Name = "hive-conf-volume",
                                    ConfigMap = new ConfigMapVolumeSourceArgs
                                    {
                                        Name = hiveConfigMap.Metadata.Apply(m => m.Name)
                                    }
                                },
                                new VolumeArgs
                                {
                                    Name = "flink-conf-volume",
                                    ConfigMap = new ConfigMapVolumeSourceArgs
                                    {
                                        Name = flinkConfConfigMap.Metadata.Apply(m => m.Name)
                                    }
                                }
                            }
                        }
                    },
                    Service = new ServiceSpecArgs
                    {
                        Type = "ClusterIP",
                        Port = 8081,
                        TargetPort = 8081
                    }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });
            var flinkGatewayService = new Service("flink-sql-gateway-service", new ServiceArgs
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "flink-sql-gateway-service",
                    Namespace = Constants.Namespace
                },
                Spec = new Pulumi.Kubernetes.Types.Inputs.Core.V1.ServiceSpecArgs
                {
                    Type = "ClusterIP",
                    Ports = new List<Pulumi.Kubernetes.Types.Inputs.Core.V1.ServicePortArgs>
                    {
                        new Pulumi.Kubernetes.Types.Inputs.Core.V1.ServicePortArgs
                        {
                            Name = "hiveserver2",
                            Port = 10000,
                            TargetPort = 10000
                        }
                    },
                    Selector = new Dictionary<string, string>
                    {
                        { "app", "basic-checkpoint-ha-sql-example" },
                        { "component", "jobmanager" }
                    }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });
        }
    }
}