using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.Polaris;

public class Polaris : ComponentResource
{
    public Polaris(string manifestsRoot) : base("secret", "secret")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/polaris"
        }, new CustomResourceOptions
        {
            Parent = this
        });
        
        var postgresCredSecret = new ExternalSecret("postgres-secret", new ()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "postgres-secret",
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
                    Name = "postgres-secret"
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
            Provider = provider,
            Parent = this,
        });
        
        // var postgresPod = new Pod("postgres-pod", new PodArgs()
        // {
        //     Metadata = new ObjectMetaArgs
        //     {
        //         Name = "postgres-pod",
        //         Namespace = "polaris",
        //         Labels = 
        //         {
        //             { "app", "postgres" }
        //         }
        //     },
        //     Spec = new PodSpecArgs
        //     {
        //         Containers = 
        //         {
        //             new ContainerArgs
        //             {
        //                 Name = "postgres-container",
        //                 Image = "postgres:14", 
        //                 Ports = 
        //                 {
        //                     new ContainerPortArgs
        //                     {
        //                         ContainerPortValue = 5432 
        //                     }
        //                 },
        //                 Env = 
        //                 {
        //                     
        //                     new EnvVarArgs
        //                     {
        //                         Name = "POSTGRES_USER",
        //                         ValueFrom = new EnvVarSourceArgs
        //                         {
        //                             SecretKeyRef = new SecretKeySelectorArgs
        //                             {
        //                                 Name = postgresCredSecret.Metadata.Apply(m => m.Name),
        //                                 Key = "username"
        //                             }
        //                         }
        //                     },
        //                     new EnvVarArgs
        //                     {
        //                         Name = "POSTGRES_PASSWORD",
        //                         ValueFrom = new EnvVarSourceArgs
        //                         {
        //                             SecretKeyRef = new SecretKeySelectorArgs
        //                             {
        //                                 Name = postgresCredSecret.Metadata.Apply(m => m.Name),
        //                                 Key = "password"
        //                             }
        //                         }
        //                     },
        //                 }
        //             }
        //         }
        //     }
        // }, new CustomResourceOptions
        // {
        //     Parent = this,
        //     DependsOn = new[] { postgresCredSecret }
        // });
            
            
        var bucketSecret = new ExternalSecret("bucket-secret", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "s3-credentials",
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
                    Name = "s3-credentials"
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
            Provider = provider,
            Parent = this,
        });
        
        var polarisKeyPair = new ExternalSecret("polaris-key-pair", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "polaris-key-pair",
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
                    Name = "polaris-key-pair"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = "id:polaris-key-pair"
                    }
                }
            }
        }, new()
        {
            Provider = provider,
            Parent = this,
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
            Provider = provider,
            Parent = this
        });
    }
}

