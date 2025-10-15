using System.Collections.Generic;
using Pulumi.Crds.ExternalSecrets;
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

		var registryWriteCredentials = new ExternalSecret("container-registry-write-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "container-registry-write-credentials",
                Namespace = "kafka-connect",
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
                            [".dockerconfigjson"] = "{\"auths\":{\"rg.nl-ams.scw.cloud\":{\"auth\":\"{{ printf \"%s:%s\" .SCALEWAY_ACCESS_KEY .SCALEWAY_SECRET_KEY | b64enc }}\"}}}"
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
                Namespace = "kafka-connect",
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
                            [".dockerconfigjson"] = "{\"auths\":{\"rg.nl-ams.scw.cloud\":{\"auth\":\"{{ printf \"%s:%s\" .SCALEWAY_ACCESS_KEY .SCALEWAY_SECRET_KEY | b64enc }}\"}}}"
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
                Namespace = "kafka-connect",
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
                Namespace = "kafka-connect",
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
                Namespace = "kafka-connect",
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

 		var kafkaConnect = new Kubernetes.ApiExtensions.CustomResource("kafka-connect",
            new KafkaConnectArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "universal-kafka-connect",
                    Namespace = "kafka-connect",
                    Labels = new Dictionary<string, string>
                    {
                        { "app", "universal-kafka" }
                    },
                    Annotations = new Dictionary<string, string>
                    {
                        { "strimzi.io/use-connector-resources", "true" }
                    }
                },
                Spec = new Dictionary<string, object>
                {
                    ["replicas"] = 1,
                    ["bootstrapServers"] = "warpstream-agent.warpstream.svc.cluster.local:9092",
                    ["image"] = "ttl.sh/hxt-kafka-connect-amd64:24h",
                    ["config"] = new Dictionary<string, object>
                    {
                        ["group.id"] = "infra.kafka.connect.init",
                        ["offset.storage.topic"] = "infra.kafka.connect.offsets",
                        ["config.providers"] = "env",
                        ["config.providers.env.class"] = "org.apache.kafka.common.config.provider.EnvVarConfigProvider",
                        ["config.storage.topic"] = "infra.kafka.connect.configs",
                        ["status.storage.topic"] = "infra.kafka.connect.status",
                        ["offset.storage.replication.factor"] = 1,
                        ["config.storage.replication.factor"] = 1,
                        ["status.storage.replication.factor"] = 1,
                        ["producer.batch.size"] = 32768,
                        ["producer.linger.ms"] = 100,
                        ["consumer.max.poll.records"] = 500
                    },
                    ["resources"] = new Dictionary<string, object>
                    {
                        ["requests"] = new Dictionary<string, string>
                        {
                            ["cpu"] = "3",
                            ["memory"] = "6Gi"
                        },
                        ["limits"] = new Dictionary<string, string>
                        {
                            ["cpu"] = "4",
                            ["memory"] = "8Gi"
                        }
                    },
                    ["jvmOptions"] = new Dictionary<string, object>
                    {
                        ["-Xmx"] = "5G"
                    },
                    ["template"] = new Dictionary<string, object>
                    {
                        ["connectContainer"] = new Dictionary<string, object>
                        {
                            ["env"] = new List<Dictionary<string, object>>
                            {
                                new Dictionary<string, object>
                                {
                                    ["name"] = "AWS_ACCESS_KEY_ID",
                                    ["valueFrom"] = new Dictionary<string, object>
                                    {
                                        ["secretKeyRef"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "iceberg-bucket-credentials",
                                            ["key"] = "AWS_ACCESS_KEY"
                                        }
                                    }
                                },
                                new Dictionary<string, object>
                                {
                                    ["name"] = "AWS_SECRET_ACCESS_KEY",
                                    ["valueFrom"] = new Dictionary<string, object>
                                    {
                                        ["secretKeyRef"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "iceberg-bucket-credentials",
                                            ["key"] = "AWS_SECRET_KEY"
                                        }
                                    }
                                },
                                new Dictionary<string, object>
                                {
                                    ["name"] = "AWS_REGION",
                                    ["valueFrom"] = new Dictionary<string, object>
                                    {
                                        ["secretKeyRef"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "iceberg-bucket-credentials",
                                            ["key"] = "AWS_REGION"
                                        }
                                    }
                                },
                                new Dictionary<string, object>
                                {
                                    ["name"] = "POLARIS_PASSWORD",
                                    ["valueFrom"] = new Dictionary<string, object>
                                    {
                                        ["secretKeyRef"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "polaris-root-password",
                                            ["key"] = "polaris-root-password"
                                        }
                                    }
                                },
                                new Dictionary<string, object>
                                {
                                    ["name"] = "POSTGRES_HOST",
                                    ["valueFrom"] = new Dictionary<string, object>
                                    {
                                        ["secretKeyRef"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "pricefiles-db-credentials",
                                            ["key"] = "host"
                                        }
                                    }
                                },
                                new Dictionary<string, object>
                                {
                                    ["name"] = "POSTGRES_PORT",
                                    ["valueFrom"] = new Dictionary<string, object>
                                    {
                                        ["secretKeyRef"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "pricefiles-db-credentials",
                                            ["key"] = "port"
                                        }
                                    }
                                },
                                new Dictionary<string, object>
                                {
                                    ["name"] = "POSTGRES_USER",
                                    ["valueFrom"] = new Dictionary<string, object>
                                    {
                                        ["secretKeyRef"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "pricefiles-db-credentials",
                                            ["key"] = "username"
                                        }
                                    }
                                },
                                new Dictionary<string, object>
                                {
                                    ["name"] = "POSTGRES_PASSWORD",
                                    ["valueFrom"] = new Dictionary<string, object>
                                    {
                                        ["secretKeyRef"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "pricefiles-db-credentials",
                                            ["key"] = "password"
                                        }
                                    }
                                },
                                new Dictionary<string, object>
                                {
                                    ["name"] = "POSTGRES_DB",
                                    ["valueFrom"] = new Dictionary<string, object>
                                    {
                                        ["secretKeyRef"] = new Dictionary<string, object>
                                        {
                                            ["name"] = "pricefiles-db-credentials",
                                            ["key"] = "dbname"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });
    }

    private class KafkaConnectArgs : Kubernetes.ApiExtensions.CustomResourceArgs
    {
        public KafkaConnectArgs() : base("kafka.strimzi.io/v1beta2", "KafkaConnect")
        {
        }

        [Input("spec")] public Dictionary<string, object>? Spec { get; set; }
    }
}