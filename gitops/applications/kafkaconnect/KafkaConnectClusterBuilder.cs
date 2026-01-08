using System.Collections.Generic;
using Pulumi;
using Pulumi.Crds.KafkaConnect;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

/// <summary>
/// Builder for creating KafkaConnect cluster resources with sensible defaults.
/// Simplifies the creation of Kafka Connect clusters with proper configuration.
/// </summary>
public class KafkaConnectClusterBuilder
{
    private readonly string _clusterName;
    private string _bootstrapServers = "warpstream-agent.warpstream.svc.cluster.local:9092";
    private string _image = "ttl.sh/hxt-kafka-connect-amd64:24h";
    private int _replicas = 1;
    private string _namespace = "kafka-connect";
    
    // Metrics configuration
    private string? _metricsConfigMapName;
    private string? _metricsConfigMapKey;

    // Resource limits
    private string _cpuRequest = "2";
    private string _memoryRequest = "4Gi";
    private string _cpuLimit = "2";
    private string _memoryLimit = "6Gi";
    private string _jvmMaxHeap = "4G";

    // Kafka Connect worker configuration
    private string _groupIdPrefix;
    private int _replicationFactor = 1;

    /// <summary>
    /// Create a new Kafka Connect cluster builder.
    /// </summary>
    /// <param name="clusterName">Name of the Kafka Connect cluster (e.g., "m3-kafka-connect")</param>
    public KafkaConnectClusterBuilder(string clusterName)
    {
        _clusterName = clusterName;
        _groupIdPrefix = clusterName.Replace("-kafka-connect", "");
    }

    /// <summary>
    /// Set the Kafka bootstrap servers
    /// </summary>
    public KafkaConnectClusterBuilder WithBootstrapServers(string bootstrapServers)
    {
        _bootstrapServers = bootstrapServers;
        return this;
    }

    /// <summary>
    /// Set the Kafka Connect container image
    /// </summary>
    public KafkaConnectClusterBuilder WithImage(string image)
    {
        _image = image;
        return this;
    }

    /// <summary>
    /// Set the number of replicas (default: 1)
    /// </summary>
    public KafkaConnectClusterBuilder WithReplicas(int replicas)
    {
        _replicas = replicas;
        return this;
    }

    /// <summary>
    /// Set the namespace (default: "kafka-connect")
    /// </summary>
    public KafkaConnectClusterBuilder WithNamespace(string ns)
    {
        _namespace = ns;
        return this;
    }

    /// <summary>
    /// Configure Prometheus metrics via JMX Exporter
    /// </summary>
    public KafkaConnectClusterBuilder WithMetricsConfig(string configMapName, string configMapKey)
    {
        _metricsConfigMapName = configMapName;
        _metricsConfigMapKey = configMapKey;
        return this;
    }

    /// <summary>
    /// Set resource requests and limits
    /// </summary>
    public KafkaConnectClusterBuilder WithResources(
        string cpuRequest, string memoryRequest,
        string cpuLimit, string memoryLimit,
        string jvmMaxHeap)
    {
        _cpuRequest = cpuRequest;
        _memoryRequest = memoryRequest;
        _cpuLimit = cpuLimit;
        _memoryLimit = memoryLimit;
        _jvmMaxHeap = jvmMaxHeap;
        return this;
    }

    /// <summary>
    /// Build and create the KafkaConnect cluster resource.
    /// </summary>
    public ComponentResource Build(string manifestsRoot)
    {
        var componentResource = new ComponentResource(_clusterName, _clusterName);

        var provider = new Pulumi.Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = componentResource
        });
        var spec = new Dictionary<string, object>
        {
            ["replicas"] = _replicas,
            ["bootstrapServers"] = _bootstrapServers,
            ["image"] = _image,

            // Kafka Connect worker configuration
            ["config"] = new Dictionary<string, object>
            {
                ["group.id"] = $"{_groupIdPrefix}.kafka.connect.init",
                ["offset.storage.topic"] = $"{_groupIdPrefix}.kafka.connect.offsets",
                ["config.storage.topic"] = $"{_groupIdPrefix}.kafka.connect.configs",
                ["status.storage.topic"] = $"{_groupIdPrefix}.kafka.connect.status",

                ["config.providers"] = "env",
                ["config.providers.env.class"] = "org.apache.kafka.common.config.provider.EnvVarConfigProvider",

                ["offset.storage.replication.factor"] = _replicationFactor,
                ["config.storage.replication.factor"] = _replicationFactor,
                ["status.storage.replication.factor"] = _replicationFactor,

                // Session and timeouts
                ["session.timeout.ms"] = 120000,
                ["request.timeout.ms"] = 60000,
                ["heartbeat.interval.ms"] = 3000,
                ["worker.sync.timeout.ms"] = 3000,

                // Fetching
                ["fetch.max.wait.ms"] = 10000,
                ["max.poll.interval.ms"] = 300000,
                ["metadata.max.age.ms"] = 60000,

                // Consumer tuning
                ["consumer.group.instance.id"] = $"{_clusterName}-connect-0",
                ["consumer.fetch.max.wait.ms"] = 10000,
                ["consumer.fetch.max.bytes"] = 50242880,
                ["consumer.max.partition.fetch.bytes"] = 50242880,
                ["consumer.request.timeout.ms"] = 60000,
                ["consumer.session.timeout.ms"] = 45000,
                ["consumer.metadata.max.age.ms"] = 60000,
                ["consumer.retry.backoff.ms"] = 1000,

                // Producer tuning
                ["producer.batch.size"] = 100000,
                ["producer.linger.ms"] = 100,
                ["producer.buffer.memory"] = 128000000,
                ["producer.request.timeout.ms"] = 60000,
                ["producer.compression.type"] = "lz4",
                ["producer.metadata.max.age.ms"] = 60000,
                ["producer.retry.backoff.ms"] = 1000,
            },

            ["resources"] = new Dictionary<string, object>
            {
                ["requests"] = new Dictionary<string, string>
                {
                    ["cpu"] = _cpuRequest,
                    ["memory"] = _memoryRequest
                },
                ["limits"] = new Dictionary<string, string>
                {
                    ["cpu"] = _cpuLimit,
                    ["memory"] = _memoryLimit
                }
            },

            ["jvmOptions"] = new Dictionary<string, object>
            {
                ["-Xmx"] = _jvmMaxHeap
            },

            ["template"] = new Dictionary<string, object>
            {
                ["connectContainer"] = new Dictionary<string, object>
                {
                    ["env"] = new List<Dictionary<string, object>>
                    {
                        CreateEnvVar("AWS_ACCESS_KEY_ID", "iceberg-bucket-credentials", "AWS_ACCESS_KEY"),
                        CreateEnvVar("AWS_SECRET_ACCESS_KEY", "iceberg-bucket-credentials", "AWS_SECRET_KEY"),
                        CreateEnvVar("AWS_REGION", "iceberg-bucket-credentials", "AWS_REGION"),
                        CreateEnvVar("POLARIS_PASSWORD", "polaris-root-password", "polaris-root-password"),
                    }
                }
            }
        };

        // Add metrics configuration if provided
        if (!string.IsNullOrEmpty(_metricsConfigMapName) && !string.IsNullOrEmpty(_metricsConfigMapKey))
        {
            spec["metricsConfig"] = new Dictionary<string, object>
            {
                ["type"] = "jmxPrometheusExporter",
                ["valueFrom"] = new Dictionary<string, object>
                {
                    ["configMapKeyRef"] = new Dictionary<string, string>
                    {
                        ["name"] = _metricsConfigMapName,
                        ["key"] = _metricsConfigMapKey
                    }
                }
            };
        }


        new Pulumi.Crds.KafkaConnect.KafkaConnect(_clusterName, new KafkaConnectArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = _clusterName,
                Namespace = _namespace,
                Labels = new Dictionary<string, string>
                {
                    { "app", _clusterName.Replace("-kafka-connect", "-kafka") }
                },
                Annotations = new Dictionary<string, string>
                {
                    { "strimzi.io/use-connector-resources", "true" }
                }
            },
            Spec = new KafkaConnectSpecArgs
            {
                Replicas = _replicas,
                BootstrapServers = _bootstrapServers,
                Image = _image,
                Config = spec["config"],
                MetricsConfig = spec.ContainsKey("metricsConfig") ? spec["metricsConfig"] : null,
                Resources = new ResourceRequirementsArgs
                {
                    Requests = new InputMap<string>
                    {
                        { "cpu", _cpuRequest },
                        { "memory", _memoryRequest }
                    },
                    Limits = new InputMap<string>
                    {
                        { "cpu", _cpuLimit },
                        { "memory", _memoryLimit }
                    }
                },
                JvmOptions = new InputMap<string>
                {
                    { "-Xmx", _jvmMaxHeap }
                },
                Template = new KafkaConnectTemplateArgs
                {
                    ConnectContainer = new ConnectContainerArgs
                    {
                        Env = new InputList<EnvVarArgs>
                        {
                            CreateEnvVarArgs("AWS_ACCESS_KEY_ID", "iceberg-bucket-credentials", "AWS_ACCESS_KEY"),
                            CreateEnvVarArgs("AWS_SECRET_ACCESS_KEY", "iceberg-bucket-credentials", "AWS_SECRET_KEY"),
                            CreateEnvVarArgs("AWS_REGION", "iceberg-bucket-credentials", "AWS_REGION"),
                            CreateEnvVarArgs("POLARIS_PASSWORD", "polaris-root-password", "polaris-root-password"),
                        }
                    }
                }
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = componentResource
        });

        return componentResource;
    }

    private static Dictionary<string, object> CreateEnvVar(string name, string secretName, string secretKey)
    {
        return new Dictionary<string, object>
        {
            ["name"] = name,
            ["valueFrom"] = new Dictionary<string, object>
            {
                ["secretKeyRef"] = new Dictionary<string, object>
                {
                    ["name"] = secretName,
                    ["key"] = secretKey
                }
            }
        };
    }

    private static EnvVarArgs CreateEnvVarArgs(string name, string secretName, string secretKey)
    {
        return new EnvVarArgs
        {
            Name = name,
            ValueFrom = new EnvVarSourceArgs
            {
                SecretKeyRef = new SecretKeySelectorArgs
                {
                    Name = secretName,
                    Key = secretKey
                }
            }
        };
    }
}