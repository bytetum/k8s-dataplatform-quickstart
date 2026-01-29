using System.Collections.Generic;
using Pulumi;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

internal class KafkaConnect : ComponentResource
{
    public KafkaConnect(string manifestsRoot) : base(
        "kafkaconnect",
        "kafkaconnect")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = this
        });

        // ========================================================================
        // 1. EXTERNAL SECRETS - Credentials from external secret store
        // ========================================================================

        var registryWriteCredentials = new ExternalSecret("container-registry-write-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "container-registry-write-credentials",
                Namespace = Constants.KafkaConnectNamespace,
            },
            Spec = new ExternalSecretSpecArgs()
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = "shared-secret-store",
                    Kind = "ClusterSecretStore"
                },
                Target = new ExternalSecretSpecTargetArgs()
                {
                    Name = "container-registry-write-credentials",
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
                        Key = "id:f11e62c3-85f0-40c9-82a8-b6f1d7ce7932"
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        var registryReadCredentials = new ExternalSecret("container-registry-read-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "container-registry-read-credentials",
                Namespace = Constants.KafkaConnectNamespace,
            },
            Spec = new ExternalSecretSpecArgs()
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = "shared-secret-store",
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
                        Key = "id:1cf21d4a-b561-4353-9981-fecafe592689"
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        var polarisRootPassword = new ExternalSecret("polaris-root-password", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "polaris-root-password",
                Namespace = Constants.KafkaConnectNamespace,
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
                    Name = "polaris-root-password"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = "id:polaris-root-password"
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        var icebergBucketCredentials = new ExternalSecret("iceberg-bucket-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "iceberg-bucket-credentials",
                Namespace = Constants.KafkaConnectNamespace,
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
                    Name = "iceberg-bucket-credentials"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = "id:c2f85be8-7fd0-402d-8229-6de987bcbbb4"
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        var postgresCredentials = new ExternalSecret("pricefiles-db-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "pricefiles-db-credentials",
                Namespace = Constants.KafkaConnectNamespace,
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
                    Name = "pricefiles-db-credentials"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = "id:86fac104-7ff1-47d8-8cd1-7e2d74dbc0fd" // Replace with actual secret ID
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        var schemaRegistryCredentials = new ExternalSecret("schema-registry-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "schema-registry-credentials",
                Namespace = Constants.KafkaConnectNamespace,
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
                    Name = "schema-registry-credentials"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = "id:schema-registry-credentials"
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        var metricsConfig = new ConfigMap("kafka-connect-metrics", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "kafka-connect-metrics",
                Namespace = "kafka-connect"
            },
            Data = new InputMap<string>
            {
                {
                    "metrics-config.yml",
                    $"""
                         lowercaseOutputName: true
                         lowercaseOutputLabelNames: true
                         rules:
                         # DLQ metrics (most important)
                         - pattern: kafka.connect<type=task-error-metrics, connector=(.+), task=(.+)><>(total-record-errors|deadletterqueue-produce-requests)
                           name: kafka_connect_task_error_$3
                           labels:
                             connector: "$1"
                             task: "$2"

                         # Connector status
                         - pattern: kafka.connect<type=connect-worker-metrics><>(connector-count|task-count)
                           name: kafka_connect_worker_$1

                         # Task metrics
                         - pattern: kafka.connect<type=sink-task-metrics, connector=(.+), task=(.+)><>(sink-record-send-total|offset-commit-completion-total)
                           name: kafka_connect_sink_task_$3
                           labels:
                             connector: "$1"
                             task: "$2"
                         """.Replace("\r\n", "\n")
                }
            }
        }, new CustomResourceOptions
        {
            Parent = this,
            Provider = provider
        });
    }
}