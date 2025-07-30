using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.Polaris;

public class Polaris : ComponentResource
{
    public Polaris(string manifestsRoot) : base("polaris", "polaris")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/polaris",
        }, new CustomResourceOptions
        {
            Parent = this
        });
            
            
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
            Parent = this,
            Provider = provider
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
                        Key = "id:842cb98e-9786-4cc6-9af7-424f9278d802"
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });
    }
}

