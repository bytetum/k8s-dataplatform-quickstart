using System.Collections.Generic;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.postgres;

public class Postgres : ComponentResource
{
    public Postgres(string manifestsRoot) : base("postgres", "postgres")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/polaris",
        }, new CustomResourceOptions
        {
            Parent = this
        });

        var postgresCredentials = new ExternalSecret("polaris-postgres-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "polaris-postgres-credentials",
                Namespace = Constants.PolarisNamespace,
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs
                {
                    Name = SecretSources.StoreName,
                    Kind = "ClusterSecretStore",
                },
                Target = new ExternalSecretSpecTargetArgs
                {
                    Name = "polaris-postgres-credentials",
                    Template = Constants.IsKindLocal
                        ? new ExternalSecretSpecTargetTemplateArgs
                        {
                            Data = new Dictionary<string, string>
                            {
                                ["username"] = "{{ .username }}",
                                ["password"] = "{{ .password }}",
                                ["jdbcUrl"] = $"jdbc:postgresql://postgres-service:5432/{Constants.PolarisDatabase}",
                            },
                        }
                        : null!,
                },
                DataFrom = new ExternalSecretSpecDataFromArgs
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs
                    {
                        Key = SecretSources.PolarisPostgresCredentials,
                    },
                },
            },
        }, new CustomResourceOptions
        {
            Parent = this,
            Provider = provider,
        });

        // Kind-local keeps catalog metadata on a durable PVC.  mac-local
        // keeps the original pod-local database layout.
        PersistentVolumeClaim? postgresData = null;
        if (Constants.IsKindLocal)
        {
        postgresData = new PersistentVolumeClaim("polaris-postgres-data", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "polaris-postgres-data",
                Namespace = Constants.PolarisNamespace,
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
        }

        // Replace Pod with Deployment
        var postgresDeployment = new Pulumi.Kubernetes.Apps.V1.Deployment("postgres-deployment", new DeploymentArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "postgres-deployment",
                Namespace = Constants.PolarisNamespace,
                Labels =
                {
                    { "app", "postgres" }
                }
            },
            Spec = new Pulumi.Kubernetes.Types.Inputs.Apps.V1.DeploymentSpecArgs
            {
                Selector = new Pulumi.Kubernetes.Types.Inputs.Meta.V1.LabelSelectorArgs
                {
                    MatchLabels =
                    {
                        { "app", "postgres" }
                    }
                },
                Replicas = 1,
                Template = new PodTemplateSpecArgs
                {
                    Metadata = new ObjectMetaArgs
                    {
                        Labels =
                        {
                            { "app", "postgres" }
                        }
                    },
                    Spec = new PodSpecArgs
                    {
                        Containers =
                        {
                            new ContainerArgs
                            {
                                Name = "postgres-container",
                                Image = Constants.IsKindLocal ? "postgres:14.18-bookworm" : "postgres:14",
                                Resources = Constants.IsKindLocal ? new ResourceRequirementsArgs
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
                                } : null!,
                                Ports =
                                {
                                    new ContainerPortArgs
                                    {
                                        ContainerPortValue = 5432
                                    }
                                },
                                VolumeMounts = Constants.IsKindLocal
                                    ? new InputList<VolumeMountArgs>
                                    {
                                        new VolumeMountArgs
                                        {
                                            Name = "postgres-data",
                                            MountPath = "/var/lib/postgresql/data",
                                        },
                                    }
                                    : null!,
                                Env = Constants.IsKindLocal
                                    ? new InputList<EnvVarArgs>
                                    {
                                    new EnvVarArgs
                                    {
                                        Name = "POSTGRES_DB",
                                        Value = Constants.PolarisDatabase
                                    },
                                    new EnvVarArgs
                                    {
                                        Name = "POSTGRES_USER",
                                        ValueFrom = new EnvVarSourceArgs
                                        {
                                            SecretKeyRef = new SecretKeySelectorArgs
                                            {
                                                Name = "polaris-postgres-credentials",
                                                Key = "username",
                                            },
                                        },
                                    },
                                    new EnvVarArgs
                                    {
                                        Name = "POSTGRES_PASSWORD",
                                        ValueFrom = new EnvVarSourceArgs
                                        {
                                            SecretKeyRef = new SecretKeySelectorArgs
                                            {
                                                Name = "polaris-postgres-credentials",
                                                Key = "password"
                                            }
                                        }
                                    },
                                    }
                                    : new InputList<EnvVarArgs>
                                    {
                                        new EnvVarArgs
                                        {
                                            Name = "POSTGRES_DB",
                                            Value = "polaris"
                                        },
                                        new EnvVarArgs
                                        {
                                            Name = "POSTGRES_HOST",
                                            ValueFrom = new EnvVarSourceArgs
                                            {
                                                SecretKeyRef = new SecretKeySelectorArgs
                                                {
                                                    Name = postgresCredentials.Metadata.Apply(m => m.Name),
                                                    Key = "db-address"
                                                }
                                            }
                                        },
                                        new EnvVarArgs
                                        {
                                            Name = "POSTGRES_USER",
                                            ValueFrom = new EnvVarSourceArgs
                                            {
                                                SecretKeyRef = new SecretKeySelectorArgs
                                                {
                                                    Name = postgresCredentials.Metadata.Apply(m => m.Name),
                                                    Key = "username"
                                                }
                                            }
                                        },
                                        new EnvVarArgs
                                        {
                                            Name = "POSTGRES_PASSWORD",
                                            ValueFrom = new EnvVarSourceArgs
                                            {
                                                SecretKeyRef = new SecretKeySelectorArgs
                                                {
                                                    Name = postgresCredentials.Metadata.Apply(m => m.Name),
                                                    Key = "password"
                                                }
                                            }
                                        },
                                    },
                            }
                        },
                        Volumes = Constants.IsKindLocal
                            ? new InputList<VolumeArgs>
                            {
                                new VolumeArgs
                                {
                                    Name = "postgres-data",
                                    PersistentVolumeClaim = new PersistentVolumeClaimVolumeSourceArgs
                                    {
                                        ClaimName = postgresData!.Metadata.Apply(m => m.Name),
                                    },
                                },
                            }
                            : null!,
                    }
                }
            }
        }, new CustomResourceOptions
        {
            Parent = this,
            Provider = provider,
            DependsOn = Constants.IsKindLocal
                ? new Resource[] { postgresCredentials, postgresData! }
                : new Resource[] { postgresCredentials }
        });

        var postgresService = new Service("postgres-service", new ServiceArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "postgres-service",
                Namespace = Constants.PolarisNamespace
            },
            Spec = new ServiceSpecArgs
            {
                Type = "ClusterIP",
                Selector =
                {
                    { "app", "postgres" }
                },
                Ports =
                {
                    new ServicePortArgs
                    {
                        Protocol = "TCP",
                        Port = 5432,
                        TargetPort = 5432
                    }
                }
            }
        }, new CustomResourceOptions
        {
            Parent = this,
            Provider = provider
        });
    }
}
