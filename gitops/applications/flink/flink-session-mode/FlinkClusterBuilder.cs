using System.Collections.Generic;
using Pulumi.Crds.ExternalSecrets;

namespace applications.flink.flink_session_mode;

using System;
using Pulumi;
using Kubernetes.Core.V1;
using Kubernetes.Rbac.V1;
using Kubernetes.Types.Inputs.Core.V1;
using Kubernetes.Types.Inputs.Apps.V1;
using Kubernetes.Types.Inputs.Meta.V1;
using Kubernetes.Types.Inputs.Rbac.V1;

internal class FlinkClusterBuilder
{
    private string _namespace = Constants.Namespace;
    private string _manifestRoot = "";
    private int _taskSlots = 1;
    private int _taskManagerReplicas = 2;
    private string _jobManagerMemory = applications.Constants.IsKindLocal ? "512m" : "1024m";
    private string _taskManagerMemory = applications.Constants.IsKindLocal ? "768m" : "2048m";
    private int _parallelismDefault = applications.Constants.IsKindLocal ? 1 : 2;

    public FlinkClusterBuilder(string manifestsRoot)
    {
        _manifestRoot = manifestsRoot;
    }

    public FlinkClusterBuilder WithNamespace(string ns)
    {
        _namespace = ns;
        return this;
    }

    public FlinkClusterBuilder WithTaskSlots(int taskSlots)
    {
        _taskSlots = taskSlots;
        return this;
    }

    public FlinkClusterBuilder WithTaskManagerReplicas(int replicas)
    {
        _taskManagerReplicas = replicas;
        return this;
    }

    public FlinkClusterBuilder WithJobManagerMemory(string memory)
    {
        _jobManagerMemory = memory;
        return this;
    }

    public FlinkClusterBuilder WithTaskManagerMemory(string memory)
    {
        _taskManagerMemory = memory;
        return this;
    }

    public FlinkClusterBuilder WithParallelismDefault(int parallelism)
    {
        if (parallelism <= 0)
        {
            throw new ArgumentException("Parallelism must be greater than 0", nameof(parallelism));
        }

        int maxParallelism = _taskManagerReplicas * _taskSlots;
        if (parallelism > maxParallelism)
        {
            throw new ArgumentException(
                $"Parallelism ({parallelism}) cannot exceed available slots ({maxParallelism}). " +
                $"Consider increasing task manager replicas or task slots.",
                nameof(parallelism));
        }

        _parallelismDefault = parallelism;
        return this;
    }

    public ComponentResource Build()
    {
        var flinkClusterComponent = new ComponentResource("flink-session-mode", "flink-session-mode");

        var manifestsPath = $"{_manifestRoot}/flink-session-mode";
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = manifestsPath
        }, new CustomResourceOptions
        {
            Parent = flinkClusterComponent
        });

        var flinkBucketCredentials = new ExternalSecret("flink-bucket-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "flink-bucket-credentials",
                Namespace = _namespace,
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = SecretSources.StoreName,
                    Kind = "ClusterSecretStore"
                },
                Target = new ExternalSecretSpecTargetArgs()
                {
                    Name = "flink-bucket-credentials"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = SecretSources.FlinkBucketCredentials
                    }
                }
            }
        }, new()
        {
            Parent = flinkClusterComponent,
            Provider = provider
        });

        var schemaRegistryCredentials = new ExternalSecret("schema-registry-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "schema-registry-credentials",
                Namespace = _namespace,
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = SecretSources.StoreName,
                    Kind = "ClusterSecretStore"
                },
                Target = new ExternalSecretSpecTargetArgs()
                {
                    Name = "schema-registry-credentials"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = SecretSources.SchemaRegistryCredentials
                    }
                }
            }
        }, new()
        {
            Parent = flinkClusterComponent,
            Provider = provider
        });


        if (!applications.Constants.IsKindLocal)
        {
            _ = new ExternalSecret("container-registry-read-credentials", new()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "container-registry-read-credentials",
                    Namespace = _namespace,
                },
                Spec = new ExternalSecretSpecArgs()
                {
                    SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                    {
                        Name = SecretSources.StoreName,
                        Kind = "ClusterSecretStore"
                    },
                    Target = new ExternalSecretSpecTargetArgs()
                    {
                        Name = "container-registry-read-credentials",
                        Template = new ExternalSecretSpecTargetTemplateArgs()
                        {
                            Type = "kubernetes.io/dockerconfigjson",
                            Data = new Dictionary<string, string>
                            {
                                [".dockerconfigjson"] =
                                "{\"auths\":{\"rg.nl-ams.scw.cloud\":{\"auth\":\"{{ printf \"%s:%s\" .SCALEWAY_ACCESS_KEY .SCALEWAY_SECRET_KEY | b64enc }}\"}}}"
                            }
                        }
                    },
                    DataFrom = new ExternalSecretSpecDataFromArgs()
                    {
                        Extract = new ExternalSecretSpecDataFromExtractArgs()
                        {
                            Key = SecretSources.ContainerRegistryReadCredentials
                        }
                    }
                }
            }, new()
            {
                Parent = flinkClusterComponent,
                Provider = provider
            });
        }

        // Service account for JobManager and TaskManager.  Kind-local uses a
        // distinct name so it cannot adopt the existing operator binding.
        var sqlGatewayServiceAccount = applications.Constants.FlinkServiceAccount;
        var flinkRoleBindingName = applications.Constants.IsKindLocal
            ? "lakehouse-flink-role-binding-flink"
            : "flink-role-binding-flink";
        var sqlGatewayRoleName = applications.Constants.IsKindLocal
            ? "lakehouse-flink-sql-gateway-role"
            : "flink-sql-gateway-role";
        var sqlGatewayRoleBindingName = applications.Constants.IsKindLocal
            ? "lakehouse-flink-sql-gateway-rb"
            : "flink-sql-gateway-rb";
        var sqlGatewaySA = new ServiceAccount(sqlGatewayServiceAccount, new ServiceAccountArgs
        {
            Metadata = new ObjectMetaArgs { Name = sqlGatewayServiceAccount, Namespace = _namespace }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        var flinkClusterRoleBinding = new ClusterRoleBinding(flinkRoleBindingName, new ClusterRoleBindingArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = flinkRoleBindingName
            },
            RoleRef = new RoleRefArgs
            {
                ApiGroup = "rbac.authorization.k8s.io",
                Kind = "ClusterRole",
                Name = "edit"
            },
            Subjects = new InputList<SubjectArgs>
            {
                new SubjectArgs
                {
                    Kind = "ServiceAccount",
                    Name = sqlGatewayServiceAccount,
                    Namespace = _namespace
                }
            }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        var sqlGatewayRole = new ClusterRole(sqlGatewayRoleName, new ClusterRoleArgs
        {
            Metadata = new ObjectMetaArgs { Name = sqlGatewayRoleName },
            Rules = new InputList<PolicyRuleArgs>
            {
                new PolicyRuleArgs
                {
                    ApiGroups = { "" },
                    Resources = { "endpoints" },
                    Verbs = { "get", "list", "watch", "create", "update", "patch" }
                },
                new PolicyRuleArgs
                {
                    ApiGroups = { "discovery.k8s.io" },
                    Resources = { "endpointslices" },
                    Verbs = { "get", "list", "watch", "create", "update", "patch" }
                }
            }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        var sqlGatewayRB = new ClusterRoleBinding(sqlGatewayRoleBindingName, new ClusterRoleBindingArgs
        {
            Metadata = new ObjectMetaArgs { Name = sqlGatewayRoleBindingName },
            Subjects = new InputList<SubjectArgs>
            {
                new SubjectArgs
                {
                    Kind = "ServiceAccount",
                    Name = sqlGatewayServiceAccount,
                    Namespace = _namespace
                }
            },
            RoleRef = new RoleRefArgs
            {
                Kind = "ClusterRole",
                Name = sqlGatewayRoleName,
                ApiGroup = "rbac.authorization.k8s.io"
            }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        // Flink 2.1 reserves 192Mi JVM overhead and 256Mi metaspace. Those floors
        // do not fit the kind-local 512Mi/768Mi process sizes, so lower only those floors.
        var kindMemoryFloor = applications.Constants.IsKindLocal
            ? """
                         jobmanager.memory.jvm-overhead.min: 64m
                         jobmanager.memory.jvm-metaspace.size: 128m
                         taskmanager.memory.jvm-overhead.min: 64m
                         taskmanager.memory.jvm-metaspace.size: 128m
                         """
            : "";

        var configMap = new ConfigMap("flink-config", new ConfigMapArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "flink-config",
                Namespace = _namespace,
                Labels = new InputMap<string> { { "app", "flink" } }
            },
            Data = new InputMap<string>
            {
                {
                    "config.yaml",
                    $"""
                         jobmanager.rpc.address: flink-jobmanager
                         rest.address: flink-jobmanager
                         taskmanager.numberOfTaskSlots: {_taskSlots}
                         blob.server.port: 6124
                         jobmanager.rpc.port: 6123
                         taskmanager.rpc.port: 6122
                         jobmanager.memory.process.size: {_jobManagerMemory}
                         taskmanager.memory.process.size: {_taskManagerMemory}
                         {kindMemoryFloor}
                         parallelism.default: {_parallelismDefault}

                         execution.checkpointing.interval: 1min
                         execution.checkpointing.storage: filesystem
                         execution.checkpointing.dir: {applications.Constants.S3BucketPath}/flink-session-mode/checkpoints

                         state.backend.type: rocksdb
                         execution.checkpointing.incremental: true
                         execution.checkpointing.savepoint-dir: {applications.Constants.S3BucketPath}/flink-session-mode/savepoints
                         state.backend.rocksdb.localdir: /data/rocksdb
                         jobmanager.archive.fs.dir: {applications.Constants.S3BucketPath}/flink-session-mode/completed-jobs
                         high-availability.storageDir: {applications.Constants.S3BucketPath}/flink-session-mode/ha
                         kubernetes.cluster-id: {applications.Constants.FlinkSessionClusterId}
                         high-availability.type: kubernetes

                         # Prometheus metrics reporter
                         metrics.reporter.prom.factory.class: org.apache.flink.metrics.prometheus.PrometheusReporterFactory
                         
                         # OpenTelemetry metrics reporter (uncomment when OTel Collector is deployed)
                         # metrics.reporter.otel.factory.class: org.apache.flink.metrics.otel.OpenTelemetryMetricReporterFactory
                         # metrics.reporter.otel.exporter.endpoint: http://otel-collector:4317
                         """.Replace("\r\n", "\n")
                },
                {
                    "log4j-console.properties",
                    """
                        rootLogger.level = INFO
                        rootLogger.appenderRef.console.ref = ConsoleAppender
                        rootLogger.appenderRef.rolling.ref = RollingFileAppender
                        logger.pekko.name = org.apache.pekko
                        logger.pekko.level = INFO
                        logger.kafka.name= org.apache.kafka
                        logger.kafka.level = INFO
                        logger.hadoop.name = org.apache.hadoop
                        logger.hadoop.level = INFO
                        logger.zookeeper.name = org.apache.zookeeper
                        logger.zookeeper.level = INFO
                        appender.console.name = ConsoleAppender
                        appender.console.type = CONSOLE
                        appender.console.layout.type = PatternLayout
                        appender.console.layout.pattern = %d{yyyy-MM-dd HH:mm:ss,SSS} %-5p %-60c %x - %m%n
                        appender.rolling.name = RollingFileAppender
                        appender.rolling.type = RollingFile
                        appender.rolling.append = false
                        appender.rolling.fileName = ${sys:log.file}
                        appender.rolling.filePattern = ${sys:log.file}.%i
                        appender.rolling.layout.type = PatternLayout
                        appender.rolling.layout.pattern = %d{yyyy-MM-dd HH:mm:ss,SSS} %-5p %-60c %x - %m%n
                        appender.rolling.policies.type = Policies
                        appender.rolling.policies.size.type = SizeBasedTriggeringPolicy
                        appender.rolling.policies.size.size=100MB
                        appender.rolling.strategy.type = DefaultRolloverStrategy
                        appender.rolling.strategy.max = 10
                        logger.netty.name = org.jboss.netty.channel.DefaultChannelPipeline
                        logger.netty.level = OFF
                        """.Replace("\r\n", "\n")
                }
            }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        var jobManagerDeployment = new Pulumi.Kubernetes.Apps.V1.Deployment("flink-jobmanager", new DeploymentArgs
        {
            Metadata = new ObjectMetaArgs { Name = "flink-jobmanager", Namespace = _namespace },
            Spec = new DeploymentSpecArgs
            {
                Replicas = 1,
                Selector = new LabelSelectorArgs
                    { MatchLabels = new InputMap<string> { { "app", "flink" }, { "component", "jobmanager" } } },
                Template = new PodTemplateSpecArgs
                {
                    Metadata = new ObjectMetaArgs
                    {
                        Labels = new InputMap<string>
                        {
                            { "app", "flink" },
                            { "component", "jobmanager" },
                            { "metrics", "prometheus" }
                        },
                        Annotations = new InputMap<string>
                        {
                            { "prometheus.io/scrape", "true" },
                            { "prometheus.io/port", "9249" },
                            { "prometheus.io/path", "/metrics" }
                        }
                    },
                    Spec = new PodSpecArgs
                    {
                        // ImagePullSecrets = new InputList<LocalObjectReferenceArgs>
                        // {
                        //     new LocalObjectReferenceArgs { Name = "container-registry-read-credentials" }
                        // },
                        ServiceAccountName = sqlGatewaySA.Metadata.Apply(m => m.Name),
                        Containers = new ContainerArgs
                        {
                            Name = "jobmanager",
                            //Image = "rg.nl-ams.scw.cloud/b2b-data-platform-shared-registry/flink:1.20.2",
                            Image = "flink-test:2.1.1",
                            ImagePullPolicy = "Never",
                            Resources = applications.Constants.IsKindLocal ? new ResourceRequirementsArgs
                            {
                                Requests = new InputMap<string>
                                {
                                    { "cpu", "100m" },
                                    { "memory", "512Mi" },
                                },
                                Limits = new InputMap<string>
                                {
                                    { "cpu", "500m" },
                                    { "memory", "768Mi" },
                                },
                            } : null!,
                            Args = new InputList<string> { "jobmanager" },
                            Ports = new InputList<ContainerPortArgs>
                            {
                                new ContainerPortArgs { Name = "rpc", ContainerPortValue = 6123 },
                                new ContainerPortArgs { Name = "blob-server", ContainerPortValue = 6124 },
                                new ContainerPortArgs { Name = "webui", ContainerPortValue = 8081 },
                                new ContainerPortArgs { Name = "metrics", ContainerPortValue = 9249 }
                            },
                            LivenessProbe = new ProbeArgs
                            {
                                TcpSocket = new TCPSocketActionArgs { Port = 6123 }, InitialDelaySeconds = 30,
                                PeriodSeconds = 60
                            },
                            Env = new InputList<EnvVarArgs>
                            {
                                new EnvVarArgs
                                {
                                    Name = "AWS_ACCESS_KEY_ID",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "flink-bucket-credentials",
                                            Key = "AWS_ACCESS_KEY_ID"
                                        }
                                    }
                                },
                                new EnvVarArgs
                                {
                                    Name = "AWS_SECRET_ACCESS_KEY",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "flink-bucket-credentials",
                                            Key = "AWS_SECRET_ACCESS_KEY"
                                        }
                                    }
                                },
                                new EnvVarArgs
                                {
                                    Name = "SCHEMA_REGISTRY_USERNAME",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "schema-registry-credentials",
                                            Key = "username"
                                        }
                                    }
                                },
                                new EnvVarArgs
                                {
                                    Name = "SCHEMA_REGISTRY_PASSWORD",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "schema-registry-credentials",
                                            Key = "password"
                                        }
                                    }
                                },
                            },
                            VolumeMounts = new InputList<VolumeMountArgs>
                            {
                                new VolumeMountArgs { Name = "flink-config-volume", MountPath = "/opt/flink/conf" }
                            }
                        },
                        Volumes = new InputList<VolumeArgs>
                        {
                            new VolumeArgs
                            {
                                Name = "flink-config-volume",
                                ConfigMap = new ConfigMapVolumeSourceArgs { Name = "flink-config" }
                            }
                        }
                    }
                }
            }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        var jobManagerService = new Service("flink-jobmanager", new ServiceArgs
        {
            Metadata = new ObjectMetaArgs { Name = "flink-jobmanager", Namespace = _namespace },
            Spec = new ServiceSpecArgs
            {
                Type = "ClusterIP",
                Ports = new InputList<ServicePortArgs>
                {
                    new ServicePortArgs { Name = "rpc", Port = 6123 },
                    new ServicePortArgs { Name = "blob", Port = 6124 },
                    new ServicePortArgs { Name = "webui", Port = 8081 }
                },
                Selector = new InputMap<string> { { "app", "flink" }, { "component", "jobmanager" } }
            }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        var taskManagerDeployment = new Pulumi.Kubernetes.Apps.V1.Deployment("flink-taskmanager", new DeploymentArgs
        {
            Metadata = new ObjectMetaArgs { Name = "flink-taskmanager", Namespace = _namespace },
            Spec = new DeploymentSpecArgs
            {
                Replicas = _taskManagerReplicas,
                Selector = new LabelSelectorArgs
                    { MatchLabels = new InputMap<string> { { "app", "flink" }, { "component", "taskmanager" } } },
                Template = new PodTemplateSpecArgs
                {
                    Metadata = new ObjectMetaArgs
                    {
                        Labels = new InputMap<string>
                        {
                            { "app", "flink" },
                            { "component", "taskmanager" },
                            { "metrics", "prometheus" }
                        },
                        Annotations = new InputMap<string>
                        {
                            { "prometheus.io/scrape", "true" },
                            { "prometheus.io/port", "9249" },
                            { "prometheus.io/path", "/metrics" }
                        }
                    },
                    Spec = new PodSpecArgs
                    {
                        // ImagePullSecrets = new InputList<LocalObjectReferenceArgs>
                        // {
                        //     new LocalObjectReferenceArgs { Name = "container-registry-read-credentials" }
                        // },
                        ServiceAccountName = sqlGatewaySA.Metadata.Apply(m => m.Name),
                        Containers = new ContainerArgs
                        {
                            Name = "taskmanager",
                            //Image = "rg.nl-ams.scw.cloud/b2b-data-platform-shared-registry/flink:1.20.2",
                            Image = "flink-test:2.1.1",
                            ImagePullPolicy = "Never",
                            Resources = applications.Constants.IsKindLocal ? new ResourceRequirementsArgs
                            {
                                Requests = new InputMap<string>
                                {
                                    { "cpu", "150m" },
                                    { "memory", "768Mi" },
                                },
                                Limits = new InputMap<string>
                                {
                                    { "cpu", "750m" },
                                    { "memory", "1280Mi" },
                                },
                            } : null!,
                            Args = new InputList<string> { "taskmanager" },
                            Ports = new InputList<ContainerPortArgs>
                            {
                                new ContainerPortArgs { Name = "rpc", ContainerPortValue = 6122 },
                                new ContainerPortArgs { Name = "metrics", ContainerPortValue = 9249 }
                            },
                            LivenessProbe = new ProbeArgs
                            {
                                TcpSocket = new TCPSocketActionArgs { Port = 6122 }, InitialDelaySeconds = 30,
                                PeriodSeconds = 60
                            },
                            Env = new InputList<EnvVarArgs>
                            {
                                new EnvVarArgs
                                {
                                    Name = "AWS_ACCESS_KEY_ID",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "flink-bucket-credentials",
                                            Key = "AWS_ACCESS_KEY_ID"
                                        }
                                    }
                                },
                                new EnvVarArgs
                                {
                                    Name = "AWS_SECRET_ACCESS_KEY",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "flink-bucket-credentials",
                                            Key = "AWS_SECRET_ACCESS_KEY"
                                        }
                                    }
                                },
                                new EnvVarArgs
                                {
                                    Name = "SCHEMA_REGISTRY_USERNAME",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "schema-registry-credentials",
                                            Key = "username"
                                        }
                                    }
                                },
                                new EnvVarArgs
                                {
                                    Name = "SCHEMA_REGISTRY_PASSWORD",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "schema-registry-credentials",
                                            Key = "password"
                                        }
                                    }
                                },
                            },
                            VolumeMounts = new InputList<VolumeMountArgs>
                            {
                                new VolumeMountArgs { Name = "flink-config-volume", MountPath = "/opt/flink/conf/" },
                                new VolumeMountArgs { Name = "rocksdb-local-dir", MountPath = "/data/rocksdb" }
                            }
                        },
                        Volumes = new InputList<VolumeArgs>
                        {
                            new VolumeArgs
                            {
                                Name = "flink-config-volume",
                                ConfigMap = new ConfigMapVolumeSourceArgs { Name = "flink-config" }
                            },
                            new VolumeArgs
                            {
                                Name = "rocksdb-local-dir",
                                EmptyDir = new EmptyDirVolumeSourceArgs()
                            }
                        }
                    }
                }
            }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        var sqlGatewayDeployment = new Pulumi.Kubernetes.Apps.V1.Deployment("flink-sql-gateway", new DeploymentArgs
        {
            Metadata = new ObjectMetaArgs { Name = "flink-sql-gateway", Namespace = _namespace },
            Spec = new DeploymentSpecArgs
            {
                Replicas = 1,
                Selector = new LabelSelectorArgs
                    { MatchLabels = new InputMap<string> { { "app", "flink" }, { "component", "sql-gateway" } } },
                Template = new PodTemplateSpecArgs
                {
                    Metadata = new ObjectMetaArgs
                        { Labels = new InputMap<string> { { "app", "flink" }, { "component", "sql-gateway" } } },
                    Spec = new PodSpecArgs
                    {
                        ServiceAccountName = sqlGatewaySA.Metadata.Apply(m => m.Name),
                        // ImagePullSecrets = new InputList<LocalObjectReferenceArgs>
                        // {
                        //     new LocalObjectReferenceArgs { Name = "container-registry-read-credentials" }
                        // },
                        Containers = new ContainerArgs
                        {
                            Name = "sql-gateway",
                            SecurityContext = new SecurityContextArgs { Privileged = true, RunAsUser = 0 },
                            //Image = "rg.nl-ams.scw.cloud/b2b-data-platform-shared-registry/flink:1.20.2",
                            Image = "flink-test:2.1.1",
                            ImagePullPolicy = "Never",
                            Resources = applications.Constants.IsKindLocal ? new ResourceRequirementsArgs
                            {
                                Requests = new InputMap<string>
                                {
                                    { "cpu", "100m" },
                                    { "memory", "512Mi" },
                                },
                                Limits = new InputMap<string>
                                {
                                    { "cpu", "500m" },
                                    { "memory", "768Mi" },
                                },
                            } : null!,
                            Command = new InputList<string> { "/bin/sh", "-c" },
                            Args = new InputList<string>
                            {
                                @"/opt/flink/bin/sql-gateway.sh start -Dsql-gateway.endpoint.rest.address=0.0.0.0 &&
                                echo ""SQL Gateway daemon started. Tailing logs to keep container alive."" &&
                                sleep infinity"
                            },
                            Ports = new ContainerPortArgs { Name = "rest", ContainerPortValue = 8083 },
                            Env = new InputList<EnvVarArgs>
                            {
                                new EnvVarArgs
                                {
                                    Name = "AWS_ACCESS_KEY_ID",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "flink-bucket-credentials",
                                            Key = "AWS_ACCESS_KEY_ID"
                                        }
                                    }
                                },
                                new EnvVarArgs
                                {
                                    Name = "AWS_SECRET_ACCESS_KEY",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "flink-bucket-credentials",
                                            Key = "AWS_SECRET_ACCESS_KEY"
                                        }
                                    }
                                },
                                new EnvVarArgs
                                {
                                    Name = "SCHEMA_REGISTRY_USERNAME",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "schema-registry-credentials",
                                            Key = "username"
                                        }
                                    }
                                },
                                new EnvVarArgs
                                {
                                    Name = "SCHEMA_REGISTRY_PASSWORD",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "schema-registry-credentials",
                                            Key = "password"
                                        }
                                    }
                                },
                            },
                            VolumeMounts = new InputList<VolumeMountArgs>
                            {
                                new VolumeMountArgs { Name = "flink-config-volume", MountPath = "/opt/flink/conf" }
                            }
                        },
                        Volumes = new InputList<VolumeArgs>
                        {
                            new VolumeArgs
                            {
                                Name = "flink-config-volume",
                                ConfigMap = new ConfigMapVolumeSourceArgs { Name = "flink-config" }
                            }
                        }
                    }
                }
            }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        var sqlGatewayService = new Service("flink-sql-gateway-service", new ServiceArgs
        {
            Metadata = new ObjectMetaArgs { Name = "flink-sql-gateway-service", Namespace = _namespace },
            Spec = new ServiceSpecArgs
            {
                Type = "ClusterIP",
                Ports = new ServicePortArgs { Name = "rest", Port = 8083, TargetPort = 8083 },
                Selector = new InputMap<string> { { "app", "flink" }, { "component", "sql-gateway" } }
            }
        }, new CustomResourceOptions { Provider = provider, Parent = flinkClusterComponent });

        return flinkClusterComponent;
    }
}
