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

        var postgresCred = new ExternalSecret("polaris-postgres-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "polaris-postgres-credentials",
                Namespace = "polaris",
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
                    Name = "polaris-postgres-credentials"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = "id:842cb98e-9786-4cc6-9af7-424f9278d808"
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        // Replace Pod with Deployment
        var postgresDeployment = new Pulumi.Kubernetes.Apps.V1.Deployment("postgres-deployment", new DeploymentArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "postgres-deployment",
                Namespace = "polaris",
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
                                Image = "postgres:14",
                                Ports =
                                {
                                    new ContainerPortArgs
                                    {
                                        ContainerPortValue = 5432
                                    }
                                },
                                Env =
                                {
                                    new EnvVarArgs
                                    {
                                        Name = "POSTGRES_DB",
                                        Value = "database"
                                    },
                                    new EnvVarArgs
                                    {
                                        Name = "POSTGRES_HOST",
                                        ValueFrom = new EnvVarSourceArgs
                                        {
                                            SecretKeyRef = new SecretKeySelectorArgs
                                            {
                                                Name = postgresCred.Metadata.Apply(m => m.Name),
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
                                                Name = postgresCred.Metadata.Apply(m => m.Name),
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
                                                Name = postgresCred.Metadata.Apply(m => m.Name),
                                                Key = "password"
                                            }
                                        }
                                    },
                                }
                            }
                        }
                    }
                }
            }
        }, new CustomResourceOptions
        {
            Parent = this,
            Provider = provider,
            DependsOn = new[] { postgresCred }
        });

        var postgresService = new Service("postgres-service", new ServiceArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "postgres-service",
                Namespace = "polaris"
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