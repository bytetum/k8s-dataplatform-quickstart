using System;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Crds.FlinkDeployment;
using PodTemplateSpecArgs = Pulumi.Kubernetes.Types.Inputs.Core.V1.PodTemplateSpecArgs;


namespace applications.flink.flink_deployment;

internal class FlinkDeploymentBuilder
{
    private string _manifestRoot = "";
    private string _deploymentName = "sql-runner-example";
    private string _namespace = Constants.Namespace;
    private string _image = "flink-test:1.20.2";
    private string _flinkVersion = "v1_20";
    private int _taskSlots = 1;
    private string _jobManagerMemory = "1024m";
    private double _jobManagerCpu = 0.6;
    private string _taskManagerMemory = "2048m";
    private double _taskManagerCpu = 0.6;
    private int _jobParallelism = 1;
    private string _kafkaBootstrapServers = "warpstream-agent.default.svc.cluster.local:9092";
    private string _sqlFilePath = "";
    private string _azureBlobPath = "abfss://flink@PLACEHOLDER_STORAGE_ACCOUNT.dfs.core.windows.net";
    private string _entryClass = "";
    private string _jarFilePath = "";
    private UpgradeMode _upgradeMode = UpgradeMode.Stateless;


    public FlinkDeploymentBuilder(string manifestsRoot)
    {
        _manifestRoot = manifestsRoot;
    }

    public FlinkDeploymentBuilder WithDeploymentName(string deploymentName)
    {
        _deploymentName = deploymentName;
        return this;
    }

    public FlinkDeploymentBuilder WithNamespace(string ns)
    {
        _namespace = ns;
        return this;
    }

    public FlinkDeploymentBuilder WithImage(string image)
    {
        _image = image;
        return this;
    }

    public FlinkDeploymentBuilder WithFlinkVersion(string flinkVersion)
    {
        _flinkVersion = flinkVersion;
        return this;
    }

    public FlinkDeploymentBuilder WithTaskSlots(int taskSlots)
    {
        _taskSlots = taskSlots;
        return this;
    }

    public FlinkDeploymentBuilder WithJobManagerMemory(string memory)
    {
        _jobManagerMemory = memory;
        return this;
    }

    public FlinkDeploymentBuilder WithJobManagerCpu(double cpu)
    {
        _jobManagerCpu = cpu;
        return this;
    }

    public FlinkDeploymentBuilder WithTaskManagerMemory(string memory)
    {
        _taskManagerMemory = memory;
        return this;
    }

    public FlinkDeploymentBuilder WithTaskManagerCpu(double cpu)
    {
        _taskManagerCpu = cpu;
        return this;
    }

    public FlinkDeploymentBuilder WithJobParallelism(int parallelism)
    {
        if (parallelism <= 0)
        {
            throw new ArgumentException("Parallelism must be greater than 0", nameof(parallelism));
        }

        _jobParallelism = parallelism;
        return this;
    }

    public FlinkDeploymentBuilder WithKafkaBootstrapServers(string bootstrapServers)
    {
        _kafkaBootstrapServers = bootstrapServers;
        return this;
    }

    public FlinkDeploymentBuilder WithSqlS3Uri(string sqlFilePath)
    {
        _sqlFilePath = sqlFilePath;
        return this;
    }
    
    public FlinkDeploymentBuilder WithJarS3Uri(string jarFilePath)
    {
        _jarFilePath = jarFilePath;
        return this;
    }
    
    public FlinkDeploymentBuilder WithEntryClass(string entryClass)
    {
        _entryClass = entryClass;
        return this;
    }
    
    public FlinkDeploymentBuilder WithUpgradeMode(UpgradeMode upgradeMode)
    {
        _upgradeMode = upgradeMode;
        return this;
    }

    public ComponentResource Build()
    {
        var flinkDeploymentComponent = new ComponentResource(_deploymentName, _deploymentName);

        var manifestsPath = $"{_manifestRoot}/{_deploymentName}";
        var provider = new Pulumi.Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = manifestsPath
        }, new CustomResourceOptions
        {
            Parent = flinkDeploymentComponent
        });

        var flinkDeployment = new FlinkDeployment(_deploymentName,
            new FlinkDeploymentArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = _deploymentName,
                    Namespace = _namespace,
                    Labels = new InputMap<string>
                    {
                        { "app", _deploymentName },
                        { "component", "flink" },
                        { "metrics", "prometheus" }
                    },
                    Annotations = new InputMap<string>
                    {
                        { "prometheus.io/scrape", "true" },
                        { "prometheus.io/port", "9249" },
                        { "prometheus.io/path", "/metrics" }
                    }
                },
                Spec = new FlinkDeploymentSpecArgs
                {
                    Image = _image,
                    // ImagePullSecrets = new InputList<LocalObjectReferenceArgs>
                    // {
                    //     new LocalObjectReferenceArgs { Name = "container-registry-read-credentials" }
                    // },
                    FlinkVersion = _flinkVersion,
                    FlinkConfiguration = new FlinkConfigurationSpecArgs
                    {
                        TaskManagerNumberOfTaskSlots = _taskSlots.ToString(),
                        
                        HighAvailabilityType = "kubernetes",
                        HighAvailabilityStorageDir = $"{_azureBlobPath}/flink-apps/{_deploymentName}/ha",

                        ExecutionCheckpointingInterval = "1min",
                        ExecutionCheckpointingStorage = "filesystem",
                        ExecutionCheckpointingDir =
                            $"{_azureBlobPath}/flink-apps/{_deploymentName}/checkpoints",
                        ExecutionCheckpointingSavepointDir =
                            $"{_azureBlobPath}/flink-apps/{_deploymentName}/savepoints",
                        StateBackendType = "rocksdb",
                        ExecutionCheckpointingIncremental = true,
                        StateBackendRocksDbLocalDir = "/data/rocksdb",
                        JobManagerArchiveFsDir = $"{_azureBlobPath}/flink-common/completed-jobs",
                        KafkaBootstrapServers = _kafkaBootstrapServers,
                        
                        // Prometheus metrics reporter configuration
                        MetricsReporterPromFactory = "org.apache.flink.metrics.prometheus.PrometheusReporterFactory",
                    },
                    ServiceAccount = "flink-sql-gateway-sa",
                    JobManager = new JobManagerSpecArgs
                    {
                        Resource = new ResourceSpecArgs
                        {
                            Memory = _jobManagerMemory,
                            Cpu = _jobManagerCpu,
                        },
                    },
                    TaskManager = new TaskManagerSpecArgs
                    {
                        Resource = new ResourceSpecArgs
                        {
                            Memory = _taskManagerMemory,
                            Cpu = _taskManagerCpu
                        }
                    },
                    PodTemplate = new PodTemplateSpecArgs
                    {
                        Spec = new PodSpecArgs
                        {
                            Containers = new InputList<ContainerArgs>
                            {
                                new ContainerArgs
                                {
                                    Name = "flink-main-container",
                                    ImagePullPolicy = "Never",
                                    Env = new InputList<EnvVarArgs>
                                    {
                                        new EnvVarArgs
                                        {
                                            Name = "AZURE_STORAGE_ACCOUNT_NAME",
                                            ValueFrom = new EnvVarSourceArgs
                                            {
                                                SecretKeyRef = new SecretKeySelectorArgs
                                                {
                                                    Name = "flink-bucket-credentials",
                                                    Key = "AZURE_STORAGE_ACCOUNT_NAME"
                                                }
                                            }
                                        },
                                        new EnvVarArgs
                                        {
                                            Name = "AZURE_TENANT_ID",
                                            ValueFrom = new EnvVarSourceArgs
                                            {
                                                SecretKeyRef = new SecretKeySelectorArgs
                                                {
                                                    Name = "flink-bucket-credentials",
                                                    Key = "AZURE_TENANT_ID"
                                                }
                                            }
                                        },
                                        new EnvVarArgs
                                        {
                                            Name = "AZURE_CLIENT_ID",
                                            ValueFrom = new EnvVarSourceArgs
                                            {
                                                SecretKeyRef = new SecretKeySelectorArgs
                                                {
                                                    Name = "flink-bucket-credentials",
                                                    Key = "AZURE_CLIENT_ID"
                                                }
                                            }
                                        }
                                    },
                                    VolumeMounts = new InputList<VolumeMountArgs>
                                    {
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/data/rocksdb",
                                            Name = "rocksdb-local-dir"
                                        },
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/opt/flink/sql",
                                            Name = "flink-sql"
                                        },
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/opt/flink/jar",
                                            Name = "flink-jar"
                                        }
                                    }
                                },
                            },
                            InitContainers = new InputList<ContainerArgs>
                            {
                                new ContainerArgs
                                {
                                    Name = "init-get-jars",
                                    Image = "mcr.microsoft.com/azure-cli:latest",
                                    Env = new InputList<EnvVarArgs>
                                    {
                                        new EnvVarArgs
                                        {
                                            Name = "AZURE_STORAGE_ACCOUNT_NAME",
                                            ValueFrom = new EnvVarSourceArgs
                                            {
                                                SecretKeyRef = new SecretKeySelectorArgs
                                                {
                                                    Name = "flink-bucket-credentials",
                                                    Key = "AZURE_STORAGE_ACCOUNT_NAME"
                                                }
                                            }
                                        },
                                        new EnvVarArgs
                                        {
                                            Name = "AZURE_TENANT_ID",
                                            ValueFrom = new EnvVarSourceArgs
                                            {
                                                SecretKeyRef = new SecretKeySelectorArgs
                                                {
                                                    Name = "flink-bucket-credentials",
                                                    Key = "AZURE_TENANT_ID"
                                                }
                                            }
                                        },
                                        new EnvVarArgs
                                        {
                                            Name = "AZURE_CLIENT_ID",
                                            ValueFrom = new EnvVarSourceArgs
                                            {
                                                SecretKeyRef = new SecretKeySelectorArgs
                                                {
                                                    Name = "flink-bucket-credentials",
                                                    Key = "AZURE_CLIENT_ID"
                                                }
                                            }
                                        }
                                    },
                                    Command = new InputList<string>
                                    {
                                        "sh", "-c",
                                        // Download JAR if jarFilePath is set, otherwise download SQL script
                                        // Using az storage blob download with managed identity authentication
                                        !string.IsNullOrEmpty(_jarFilePath)
                                            ? $"az storage blob download --account-name $AZURE_STORAGE_ACCOUNT_NAME --container-name flink --name {GetBlobName(_jarFilePath)} --file /opt/flink/jar/{System.IO.Path.GetFileName(_jarFilePath)} --auth-mode login"
                                            : $"az storage blob download --account-name $AZURE_STORAGE_ACCOUNT_NAME --container-name flink --name {GetBlobName(_sqlFilePath)} --file /opt/flink/sql/{System.IO.Path.GetFileName(_sqlFilePath)} --auth-mode login"
                                    },
                                    VolumeMounts = new InputList<VolumeMountArgs>
                                    {
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/opt/flink/sql",
                                            Name = "flink-sql"
                                        },
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/opt/flink/jar",
                                            Name = "flink-jar"
                                        }
                                    }
                                }
                            },
                            Volumes = new InputList<VolumeArgs>
                            {
                                new VolumeArgs
                                {
                                    Name = "rocksdb-local-dir",
                                    EmptyDir = new EmptyDirVolumeSourceArgs()
                                },
                                new VolumeArgs
                                {
                                    Name = "flink-sql",
                                    EmptyDir = new EmptyDirVolumeSourceArgs()
                                },
                                new VolumeArgs
                                {
                                    Name = "flink-jar",
                                    EmptyDir = new EmptyDirVolumeSourceArgs()
                                }
                            }
                        }
                    },
                    Job = new JobSpecArgs
                    {
                        // Use downloaded JAR if jarFilePath is set, otherwise use default SQL runner
                        JarURI = !string.IsNullOrEmpty(_jarFilePath)
                            ? $"local:///opt/flink/jar/{System.IO.Path.GetFileName(_jarFilePath)}"
                            : "local:///opt/flink/usrlib/runner.jar",
                        EntryClass = _entryClass,
                        // Only pass SQL script args when NOT using custom JAR
                        Args = string.IsNullOrEmpty(_jarFilePath)
                            ? new InputList<string> { $"/opt/flink/sql/{System.IO.Path.GetFileName(_sqlFilePath)}" }
                            : null,
                        Parallelism = _jobParallelism,
                        UpgradeMode = _upgradeMode switch
                        {
                            UpgradeMode.Stateless => "stateless",
                            UpgradeMode.Savepoint => "savepoint",
                            UpgradeMode.LastState => "last-state",
                            _ => throw new ArgumentOutOfRangeException()
                        }
                    }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = flinkDeploymentComponent
            });

        return flinkDeploymentComponent;
    }

    /// <summary>
    /// Extracts the blob name from an Azure Blob Storage path.
    /// For abfss:// URLs, extracts the path portion after the container.
    /// For simple paths, returns the path as-is.
    /// </summary>
    private static string GetBlobName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Handle abfss:// URL format: abfss://container@account.dfs.core.windows.net/path/to/blob
        if (path.StartsWith("abfss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(path);
            // Remove leading slash from path
            return uri.AbsolutePath.TrimStart('/');
        }

        // For simple paths, return as-is
        return path;
    }

    public enum UpgradeMode
    {
        Stateless,
        Savepoint,
        LastState
    }
}