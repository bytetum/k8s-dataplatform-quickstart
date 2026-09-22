using Pulumi;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
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
                    Name = SecretSources.StoreName,
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
                        Key = SecretSources.PolarisRootPassword
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
                    Name = SecretSources.StoreName,
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
                        Key = SecretSources.IcebergBucketCredentials
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
                    Name = SecretSources.StoreName,
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
                        Key = SecretSources.PricefilesDatabaseCredentials
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        if (Constants.IsKindLocal)
        {
        // Fresh isolated CDC source database.  The kind-local seed pipeline
        // supplies its credentials; this deployment never points at the
        // legacy source slot or external host.  Logical replication is enabled
        // explicitly for Debezium and data is retained on a durable PVC.
        var sourceDatabaseInit = new ConfigMap("postgres-m3-test-init", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "postgres-m3-test-init",
                Namespace = Constants.KafkaConnectNamespace,
            },
            Data = new InputMap<string>
            {
                ["001-kind-local.sql"] = """
                    CREATE TABLE IF NOT EXISTS public.csytab (
                      ctstky text NOT NULL,
                      ctstco text NOT NULL,
                      description text,
                      updated_at timestamptz NOT NULL DEFAULT now(),
                      PRIMARY KEY (ctstky, ctstco)
                    );
                    CREATE TABLE IF NOT EXISTS public.cidmas (
                      idsuno text PRIMARY KEY,
                      idcono text NOT NULL,
                      name text,
                      updated_at timestamptz NOT NULL DEFAULT now()
                    );
                    CREATE TABLE IF NOT EXISTS public.cidven (
                      iisuno text PRIMARY KEY,
                      iisugr text NOT NULL,
                      name text,
                      updated_at timestamptz NOT NULL DEFAULT now()
                    );
                    INSERT INTO public.csytab (ctstky, ctstco, description)
                    VALUES ('ITEM', '001', 'Kind-local validation item')
                    ON CONFLICT (ctstky, ctstco) DO UPDATE
                      SET description = EXCLUDED.description, updated_at = now();
                    INSERT INTO public.cidmas (idsuno, idcono, name)
                    VALUES ('10001', '001', 'Kind-local validation customer')
                    ON CONFLICT (idsuno) DO UPDATE
                      SET idcono = EXCLUDED.idcono, name = EXCLUDED.name, updated_at = now();
                    INSERT INTO public.cidven (iisuno, iisugr, name)
                    VALUES ('20001', '001', 'Kind-local validation vendor')
                    ON CONFLICT (iisuno) DO UPDATE
                      SET iisugr = EXCLUDED.iisugr, name = EXCLUDED.name, updated_at = now();
                    ALTER TABLE public.csytab REPLICA IDENTITY FULL;
                    ALTER TABLE public.cidmas REPLICA IDENTITY FULL;
                    ALTER TABLE public.cidven REPLICA IDENTITY FULL;
                    -- The image creates POSTGRES_USER as a superuser.  That user
                    -- owns these tables and can manage the publication, so the
                    -- fresh database does not need a separate debezium role.
                    CREATE PUBLICATION kind_local_cdc_publication
                      FOR TABLE public.csytab, public.cidmas, public.cidven;
                    """
            },
        }, new()
        {
            Parent = this,
            Provider = provider,
        });

        var sourceDatabasePvc = new PersistentVolumeClaim("postgres-m3-test-data", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "postgres-m3-test-data",
                Namespace = Constants.KafkaConnectNamespace,
            },
            Spec = new PersistentVolumeClaimSpecArgs
            {
                AccessModes = new InputList<string> { "ReadWriteOnce" },
                Resources = new VolumeResourceRequirementsArgs
                {
                    Requests = new InputMap<string> { { "storage", "2Gi" } },
                },
            },
        }, new()
        {
            Parent = this,
            Provider = provider,
        });

        var sourceDatabase = new Pulumi.Kubernetes.Apps.V1.Deployment("postgres-m3-test", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "postgres-m3-test",
                Namespace = Constants.KafkaConnectNamespace,
                Labels = new InputMap<string> { { "app", "postgres-m3-test" } },
            },
            Spec = new Pulumi.Kubernetes.Types.Inputs.Apps.V1.DeploymentSpecArgs
            {
                Replicas = 1,
                Selector = new Pulumi.Kubernetes.Types.Inputs.Meta.V1.LabelSelectorArgs
                {
                    MatchLabels = new InputMap<string> { { "app", "postgres-m3-test" } },
                },
                Template = new PodTemplateSpecArgs
                {
                    Metadata = new ObjectMetaArgs
                    {
                        Labels = new InputMap<string> { { "app", "postgres-m3-test" } },
                    },
                    Spec = new PodSpecArgs
                    {
                        Containers = new InputList<ContainerArgs>
                        {
                            new ContainerArgs
                            {
                                Name = "postgres",
                                Image = "postgres:14.18-bookworm",
                                Args = new InputList<string>
                                {
                                    "postgres",
                                    "-c", "wal_level=logical",
                                    "-c", "max_replication_slots=4",
                                    "-c", "max_wal_senders=4",
                                },
                                Resources = new ResourceRequirementsArgs
                                {
                                    Requests = new InputMap<string>
                                    {
                                        { "cpu", "50m" },
                                        { "memory", "256Mi" },
                                    },
                                    Limits = new InputMap<string>
                                    {
                                        { "cpu", "500m" },
                                        { "memory", "768Mi" },
                                    },
                                },
                                Ports = new InputList<ContainerPortArgs>
                                {
                                    new ContainerPortArgs { ContainerPortValue = 5432 },
                                },
                                Env = new InputList<EnvVarArgs>
                                {
                                    CreateSecretEnvVar("POSTGRES_DB", "pricefiles-db-credentials", "dbname"),
                                    CreateSecretEnvVar("POSTGRES_USER", "pricefiles-db-credentials", "username"),
                                    CreateSecretEnvVar("POSTGRES_PASSWORD", "pricefiles-db-credentials", "password"),
                                },
                                VolumeMounts = new InputList<VolumeMountArgs>
                                {
                                    new VolumeMountArgs
                                    {
                                        Name = "postgres-data",
                                        MountPath = "/var/lib/postgresql/data",
                                    },
                                    new VolumeMountArgs
                                    {
                                        Name = "postgres-init",
                                        MountPath = "/docker-entrypoint-initdb.d",
                                    },
                                },
                            },
                        },
                        Volumes = new InputList<VolumeArgs>
                        {
                            new VolumeArgs
                            {
                                Name = "postgres-data",
                                PersistentVolumeClaim = new PersistentVolumeClaimVolumeSourceArgs
                                {
                                    ClaimName = sourceDatabasePvc.Metadata.Apply(m => m.Name),
                                },
                            },
                            new VolumeArgs
                            {
                                Name = "postgres-init",
                                ConfigMap = new ConfigMapVolumeSourceArgs
                                {
                                    Name = sourceDatabaseInit.Metadata.Apply(m => m.Name),
                                },
                            },
                        },
                    },
                },
            },
        }, new CustomResourceOptions
        {
            Parent = this,
            Provider = provider,
            DependsOn = new Resource[] { postgresCredentials, sourceDatabasePvc, sourceDatabaseInit },
        });

        _ = new Service("postgres-m3-test-service", new ServiceArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "postgres-m3-test",
                Namespace = Constants.KafkaConnectNamespace,
            },
            Spec = new ServiceSpecArgs
            {
                Type = "ClusterIP",
                Selector = new InputMap<string> { { "app", "postgres-m3-test" } },
                Ports = new InputList<ServicePortArgs>
                {
                    new ServicePortArgs { Port = 5432, TargetPort = 5432 },
                },
            },
        }, new CustomResourceOptions
        {
            Parent = this,
            Provider = provider,
            DependsOn = sourceDatabase,
        });
        }

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
            Parent = this,
            Provider = provider
        });

        var metricsConfig = new ConfigMap("kafka-connect-metrics", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "kafka-connect-metrics",
                Namespace = Constants.KafkaConnectNamespace
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

    private static EnvVarArgs CreateSecretEnvVar(string name, string secretName, string secretKey) =>
        new()
        {
            Name = name,
            ValueFrom = new EnvVarSourceArgs
            {
                SecretKeyRef = new SecretKeySelectorArgs
                {
                    Name = secretName,
                    Key = secretKey,
                },
            },
        };
}
