using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pulumi;
using Pulumi.Crds;
using Pulumi.Crds.KafkaConnect;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

public class KafkaConnectClusterBuilder
{
    private readonly string _manifestsRoot;
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

    public KafkaConnectClusterBuilder(string manifestsRoot, string clusterName)
    {
        _manifestsRoot = manifestsRoot;
        _clusterName = clusterName;
        _groupIdPrefix = clusterName.Replace("-kafka-connect", "");
    }

    public KafkaConnectClusterBuilder WithBootstrapServers(string bootstrapServers)
    {
        _bootstrapServers = bootstrapServers;
        return this;
    }

    public KafkaConnectClusterBuilder WithImage(string image)
    {
        _image = image;
        return this;
    }

    public KafkaConnectClusterBuilder WithReplicas(int replicas)
    {
        _replicas = replicas;
        return this;
    }

    public KafkaConnectClusterBuilder WithNamespace(string ns)
    {
        _namespace = ns;
        return this;
    }

    public KafkaConnectClusterBuilder WithMetricsConfig(string configMapName, string configMapKey)
    {
        _metricsConfigMapName = configMapName;
        _metricsConfigMapKey = configMapKey;
        return this;
    }

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

    public ComponentResource Build()
    {
        var componentResource = new ComponentResource(_clusterName, _clusterName);

        var provider = new Pulumi.Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{_manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = componentResource
        });
        // Kafka Connect worker configuration
        var config = new Dictionary<string, object>
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

            // Idempotent producer for at-least-once with deduplication
            // Note: Do NOT set transactional.id - source connectors don't support Kafka transactions
            ["producer.enable.idempotence"] = true,
            ["producer.acks"] = "all",
        };

        var spec = new Dictionary<string, object>
        {
            ["replicas"] = _replicas,
            ["bootstrapServers"] = _bootstrapServers,
            ["image"] = _image,
            ["config"] = config,

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
                        CreateEnvVar("SCHEMA_REGISTRY_USERNAME", "schema-registry-credentials", "username"),
                        CreateEnvVar("SCHEMA_REGISTRY_PASSWORD", "schema-registry-credentials", "password"),
                    }
                }
            }
        };

        // Metrics configuration (optional)
        Dictionary<string, object>? metricsConfig = null;
        if (!string.IsNullOrEmpty(_metricsConfigMapName) && !string.IsNullOrEmpty(_metricsConfigMapKey))
        {
            metricsConfig = new Dictionary<string, object>
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
            spec["metricsConfig"] = metricsConfig;
        }


        var kafkaConnectSpec = new KafkaConnectSpecArgs
        {
            Replicas = _replicas,
            BootstrapServers = _bootstrapServers,
            Image = _image,
            Config = config,
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
                        CreateEnvVarArgs("SCHEMA_REGISTRY_USERNAME", "schema-registry-credentials", "username"),
                        CreateEnvVarArgs("SCHEMA_REGISTRY_PASSWORD", "schema-registry-credentials", "password"),
                    }
                }
            }
        };

        if (metricsConfig != null)
        {
            kafkaConnectSpec.MetricsConfig = metricsConfig;
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
                    { "strimzi.io/use-connector-resources", "true" },
                    { "config-hash", ComputeConfigHash(spec) }
                }
            },
            Spec = kafkaConnectSpec
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

    private static string ComputeConfigHash(Dictionary<string, object> spec)
    {
        var json = JsonSerializer.Serialize(spec);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}