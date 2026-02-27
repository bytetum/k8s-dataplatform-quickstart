using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pulumi.Kubernetes.Batch.V1;
using Pulumi.Kubernetes.Types.Inputs.Batch.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

public class TopicCreationJobBuilder
{
    private Pulumi.Kubernetes.Provider _provider = null!;
    private Resource _parent = null!;
    private string _jobName = "";
    private string _namespace = Constants.KafkaConnectNamespace;
    private string _bootstrapServers = Constants.KafkaBootstrapServers;
    private readonly List<string> _topics = new();
    private string _cleanupPolicy = "compact";
    private int _minCompactionLagMs = 604800000; // 7 days
    private int _replicationFactor = 1;
    private int _partitions = 1;
    private int _backoffLimit = 3;

    public TopicCreationJobBuilder WithProvider(Pulumi.Kubernetes.Provider provider)
    {
        _provider = provider;
        return this;
    }

    public TopicCreationJobBuilder WithParent(Resource parent)
    {
        _parent = parent;
        return this;
    }

    public TopicCreationJobBuilder WithJobName(string jobName)
    {
        _jobName = jobName.Replace("_", "-");
        return this;
    }

    public TopicCreationJobBuilder WithNamespace(string ns)
    {
        _namespace = ns;
        return this;
    }

    public TopicCreationJobBuilder WithBootstrapServers(string bootstrapServers)
    {
        _bootstrapServers = bootstrapServers;
        return this;
    }

    public TopicCreationJobBuilder WithTopics(IEnumerable<string> topics)
    {
        _topics.AddRange(topics);
        return this;
    }

    public TopicCreationJobBuilder WithTopicConfig(
        string cleanupPolicy = "compact",
        int minCompactionLagMs = 604800000,
        int replicationFactor = 1,
        int partitions = 1)
    {
        _cleanupPolicy = cleanupPolicy;
        _minCompactionLagMs = minCompactionLagMs;
        _replicationFactor = replicationFactor;
        _partitions = partitions;
        return this;
    }

    public TopicCreationJobBuilder WithBackoffLimit(int backoffLimit)
    {
        _backoffLimit = backoffLimit;
        return this;
    }

    public Job Build()
    {
        if (_topics.Count == 0)
            throw new InvalidOperationException("At least one topic must be specified");

        var topicList = string.Join(" ", _topics);

        var creationScript = $$"""
            #!/bin/sh
            set -e

            BOOTSTRAP_SERVER="{{_bootstrapServers}}"
            TOPICS="{{topicList}}"
            CLEANUP_POLICY="{{_cleanupPolicy}}"
            MIN_COMPACTION_LAG_MS="{{_minCompactionLagMs}}"
            PARTITIONS={{_partitions}}
            REPLICATION_FACTOR={{_replicationFactor}}

            echo "Creating topics with cleanup.policy=$CLEANUP_POLICY"
            echo "Bootstrap server: $BOOTSTRAP_SERVER"

            for TOPIC in $TOPICS; do
                echo "Creating topic: $TOPIC"
                bin/kafka-topics.sh \
                    --bootstrap-server "$BOOTSTRAP_SERVER" \
                    --create --if-not-exists \
                    --topic "$TOPIC" \
                    --partitions "$PARTITIONS" \
                    --replication-factor "$REPLICATION_FACTOR" \
                    --config "cleanup.policy=$CLEANUP_POLICY" \
                    --config "min.compaction.lag.ms=$MIN_COMPACTION_LAG_MS"

                echo "Verifying topic: $TOPIC"
                bin/kafka-topics.sh \
                    --bootstrap-server "$BOOTSTRAP_SERVER" \
                    --describe --topic "$TOPIC"
            done

            echo "All topics created successfully"
            """.Replace("\r\n", "\n");

        return new Job($"topic-create-{_jobName}", new JobArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = $"topic-create-{_jobName}",
                Namespace = _namespace,
                Labels = new InputMap<string>
                {
                    { "app", "topic-create" },
                    { "target", _jobName }
                },
                Annotations = new InputMap<string>
                {
                    { "argocd.argoproj.io/hook", "PreSync" },
                    { "argocd.argoproj.io/hook-delete-policy", "BeforeHookCreation" },
                    { "config-hash", ComputeConfigHash() }
                }
            },
            Spec = new JobSpecArgs
            {
                BackoffLimit = _backoffLimit,
                TtlSecondsAfterFinished = 300,
                Template = new PodTemplateSpecArgs
                {
                    Spec = new PodSpecArgs
                    {
                        RestartPolicy = "Never",
                        Containers = new InputList<ContainerArgs>
                        {
                            new ContainerArgs
                            {
                                Name = "topic-create",
                                Image = "quay.io/strimzi/kafka:0.47.0-kafka-3.9.0",
                                Command = new InputList<string> { "sh", "-c", creationScript }
                            }
                        }
                    }
                }
            }
        }, new CustomResourceOptions
        {
            Provider = _provider,
            Parent = _parent
        });
    }

    private string ComputeConfigHash()
    {
        var config = new
        {
            _jobName,
            _topics,
            _cleanupPolicy,
            _minCompactionLagMs,
            _replicationFactor,
            _partitions,
            _bootstrapServers
        };
        var json = JsonSerializer.Serialize(config);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
