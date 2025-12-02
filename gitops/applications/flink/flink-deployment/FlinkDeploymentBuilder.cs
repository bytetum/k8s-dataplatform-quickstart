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
    private double _jobManagerCpu = 0.5;
    private string _taskManagerMemory = "2048m";
    private double _taskManagerCpu = 0.5;
    private int _jobParallelism = 1;
    private string _kafkaBootstrapServers = "warpstream-agent.default.svc.cluster.local:9092";
    private string _sqlFilePath = "";
    private string _s3BucketPath = "s3://local-rocksdb-test";


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

    public FlinkDeploymentBuilder WithSqlFilePath(string sqlFilePath)
    {
        _sqlFilePath = sqlFilePath;
        return this;
    }
    
    public FlinkDeploymentBuilder WithS3BucketPath(string s3BucketPath)
    {
        _s3BucketPath = s3BucketPath.TrimEnd('/');
        return this;
    }

    public ComponentResource Build()
    {
        var flinkDeploymentComponent = new ComponentResource("flink-deployment", "flink-deployment");

        var manifestsPath = $"{_manifestRoot}/flink-deployment";
        var provider = new Pulumi.Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = manifestsPath
        }, new CustomResourceOptions
        {
            Parent = flinkDeploymentComponent
        });

        var flinkDeployment = new FlinkDeployment("flink-deployment",
            new FlinkDeploymentArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = _deploymentName,
                    Namespace = _namespace,
                },
                Spec = new FlinkDeploymentSpecArgs
                {
                    Image = _image,
                    // ImagePullSecrets = new InputList<ImagePullSecretArgs>
                    // {
                    //     new ImagePullSecretArgs
                    //     {
                    //         Name = "container-registry-read-credentials"
                    //     }
                    // },
                    FlinkVersion = _flinkVersion,
                    FlinkConfiguration = new FlinkConfigurationSpecArgs
                    {
                        TaskManagerNumberOfTaskSlots = _taskSlots.ToString(),

                        HighAvailabilityType = "kubernetes",
                        HighAvailabilityStorageDir = $"{_s3BucketPath}/flink-apps/{_deploymentName}/ha",

                        ExecutionCheckpointingInterval = "1min",
                        ExecutionCheckpointingStorage = "filesystem",
                        ExecutionCheckpointingDir =
                            $"{_s3BucketPath}/flink-apps/{_deploymentName}/checkpoints",
                        ExecutionCheckpointingSavepointDir =
                            $"{_s3BucketPath}/flink-apps/{_deploymentName}/savepoints",
                        StateBackendType = "rocksdb",
                        ExecutionCheckpointingIncremental = true,
                        StateBackendRocksDbLocalDir = "/data/rocksdb",
                        JobManagerArchiveFsDir = $"{_s3BucketPath}/flink-common/completed-jobs",
                        KafkaBootstrapServers = _kafkaBootstrapServers
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
                                        }
                                    }
                                },
                            },
                            InitContainers = new InputList<ContainerArgs>
                            {
                                new ContainerArgs
                                {
                                    Name = "init-get-jars",
                                    Image = "amazon/aws-cli:latest",
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
                                        }
                                    },
                                    Command = new InputList<string>
                                    {
                                        "sh", "-c",
                                        "aws s3 cp s3://local-rocksdb-test/flink-sql-runner-script.sql /opt/flink/sql/script.sql"
                                    },
                                    VolumeMounts = new InputList<VolumeMountArgs>
                                    {
                                        new VolumeMountArgs
                                        {
                                            MountPath = "/opt/flink/sql",
                                            Name = "flink-sql"
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
                                }
                            }
                        }
                    },
                    Job = new JobSpecArgs
                    {
                        JarURI = "local:///opt/flink/usrlib/runner.jar",
                        Args = new InputList<string>
                        {
                            "/opt/flink/sql/script.sql",
                        },
                        Parallelism = _jobParallelism,
                        UpgradeMode = "last-state"
                    }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = flinkDeploymentComponent
            });

        return flinkDeploymentComponent;
    }
}