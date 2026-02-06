using Pulumi.Kubernetes.ApiExtensions;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Outputs.Core.V1;

namespace Pulumi.Crds.KafkaConnect
{
    public class KafkaConnect : Kubernetes.ApiExtensions.CustomResource
    {
        [Output("spec")] public Output<KafkaConnectSpec> Spec { get; private set; } = null!;

        public KafkaConnect(string name, KafkaConnectArgs args, CustomResourceOptions? options = null)
            : base(name, args, options)
        {
        }
    }

    [OutputType]
    public sealed class KafkaConnectSpec
    {
        [Output("replicas")] public Output<int> Replicas { get; private set; } = null!;
        [Output("bootstrapServers")] public Output<string> BootstrapServers { get; private set; } = null!;
        [Output("image")] public Output<string> Image { get; private set; } = null!;
        [Output("config")] public Output<Dictionary<string, object>> Config { get; private set; } = null!;
        [Output("resources")] public Output<ResourceRequirements> Resources { get; private set; } = null!;
        [Output("jvmOptions")] public Output<Dictionary<string, string>> JvmOptions { get; private set; } = null!;
        [Output("template")] public Output<KafkaConnectTemplate> Template { get; private set; } = null!;
    }

    public class KafkaConnectArgs : CustomResourceArgs
    {
        [Input("spec")] public Input<KafkaConnectSpecArgs>? Spec { get; set; }

        public KafkaConnectArgs()
            : base("kafka.strimzi.io/v1beta2", "KafkaConnect")
        {
        }
    }

    public class KafkaConnectSpecArgs : ResourceArgs
    {
        [Input("replicas")] public Input<int>? Replicas { get; set; }
        [Input("bootstrapServers")] public Input<string>? BootstrapServers { get; set; }
        [Input("image")] public Input<string>? Image { get; set; }
        [Input("config")] public Input<object>? Config { get; set; } // Renamed
        [Input("resources")] public Input<ResourceRequirementsArgs>? Resources { get; set; }
        [Input("jvmOptions")] public InputMap<string>? JvmOptions { get; set; }
        [Input("template")] public Input<KafkaConnectTemplateArgs>? Template { get; set; }
        [Input("metricsConfig")] public Input<object>? MetricsConfig { get; set; }
    }

    public class KafkaConnectTemplateArgs : ResourceArgs
    {
        [Input("connectContainer")] public Input<ConnectContainerArgs>? ConnectContainer { get; set; }
    }

    public class ConnectContainerArgs : ResourceArgs
    {
        [Input("env")] public InputList<EnvVarArgs>? Env { get; set; }
    }

    [OutputType]
    public sealed class KafkaConnectTemplate
    {
        [Output("connectContainer")] public Output<ConnectContainer> ConnectContainer { get; private set; } = null!;
    }

    [OutputType]
    public sealed class ConnectContainer
    {
        [Output("env")] public Output<List<EnvVar>> Env { get; private set; } = null!;
    }

    /// <summary>
    /// Configuration for the Kafka Connect Worker (Cluster level)
    /// </summary>
    public class KafkaConnectClusterConfigArgs : ResourceArgs
    {
        // ==========================================
        // Core Kafka Connect (Worker)
        // ==========================================
        [Input("group.id")] public Input<string>? GroupId { get; set; }
        [Input("offset.storage.topic")] public Input<string>? OffsetStorageTopic { get; set; }
        [Input("config.storage.topic")] public Input<string>? ConfigStorageTopic { get; set; }
        [Input("status.storage.topic")] public Input<string>? StatusStorageTopic { get; set; }
        [Input("offset.storage.replication.factor")] public Input<int>? OffsetStorageReplicationFactor { get; set; }
        [Input("config.storage.replication.factor")] public Input<int>? ConfigStorageReplicationFactor { get; set; }
        [Input("status.storage.replication.factor")] public Input<int>? StatusStorageReplicationFactor { get; set; }
        
        [Input("config.providers")] public Input<string>? ConfigProviders { get; set; }
        [Input("config.providers.env.class")] public Input<string>? ConfigProvidersEnvClass { get; set; }
        
        // Timeouts and Session
        [Input("session.timeout.ms")] public Input<int>? SessionTimeoutMs { get; set; }
        [Input("request.timeout.ms")] public Input<int>? RequestTimeoutMs { get; set; }
        [Input("heartbeat.interval.ms")] public Input<int>? HeartbeatIntervalMs { get; set; }
        [Input("worker.sync.timeout.ms")] public Input<int>? WorkerSyncTimeoutMs { get; set; }
        
        // Fetching
        [Input("fetch.max.wait.ms")] public Input<int>? FetchMaxWaitMs { get; set; }
        [Input("max.poll.interval.ms")] public Input<int>? MaxPollIntervalMs { get; set; }
        [Input("metadata.max.age.ms")] public Input<int>? MetadataMaxAgeMs { get; set; }
        
        // Consumer Tuning
        [Input("consumer.group.instance.id")] public Input<string>? ConsumerGroupInstanceId { get; set; }
        [Input("consumer.fetch.max.wait.ms")] public Input<int>? ConsumerFetchMaxWaitMs { get; set; }
        [Input("consumer.fetch.max.bytes")] public Input<int>? ConsumerFetchMaxBytes { get; set; }
        [Input("consumer.max.partition.fetch.bytes")] public Input<int>? ConsumerMaxPartitionFetchBytes { get; set; }
        [Input("consumer.request.timeout.ms")] public Input<int>? ConsumerRequestTimeoutMs { get; set; }
        [Input("consumer.session.timeout.ms")] public Input<int>? ConsumerSessionTimeoutMs { get; set; }
        [Input("consumer.metadata.max.age.ms")] public Input<int>? ConsumerMetadataMaxAgeMs { get; set; }
        [Input("consumer.retry.backoff.ms")] public Input<int>? ConsumerRetryBackoffMs { get; set; }
        
        // Producer Tuning
        [Input("producer.batch.size")] public Input<int>? ProducerBatchSize { get; set; }
        [Input("producer.linger.ms")] public Input<int>? ProducerLingerMs { get; set; }
        [Input("producer.buffer.memory")] public Input<int>? ProducerBufferMemory { get; set; }
        [Input("producer.request.timeout.ms")] public Input<int>? ProducerRequestTimeoutMs { get; set; }
        [Input("producer.compression.type")] public Input<string>? ProducerCompressionType { get; set; }
        [Input("producer.metadata.max.age.ms")] public Input<int>? ProducerMetadataMaxAgeMs { get; set; }
        [Input("producer.retry.backoff.ms")] public Input<int>? ProducerRetryBackoffMs { get; set; }
    }
}
