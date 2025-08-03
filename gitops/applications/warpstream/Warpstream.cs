using Pulumi.Crds.ExternalSecrets;

namespace applications.warpstream;

internal class Warpstream : ComponentResource
{
    public Warpstream(string manifestsRoot)
        : base("warpstream", "warpstream")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/warpstream-agent"
        }, new()
        {
            Parent = this,
        });

        var bucketSecret = new ExternalSecret("bucket-secret", new()
        {
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "warpstream-bucket-credentials",
                Namespace = "warpstream"
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs
                {
                    Name = "secret-store",
                    Kind = "ClusterSecretStore",
                },
                Target = new ExternalSecretSpecTargetArgs
                {
                    Name = "warpstream-bucket-credentials",
                },
                DataFrom = new ExternalSecretSpecDataFromArgs
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs
                    {
                        Key = "id:827b85c8-babe-4a43-8af2-dce1dd530081",
                        Version = "latest_enabled"
                    }
                }
            }
        }, new()
        {
            Provider = provider,
            Parent = this,
        });

        var apiKeySecret = new ExternalSecret("apikey-secret", new()
        {
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "warpstream-agent-apikey",
                Namespace = "warpstream",
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs
                {
                    Name = "secret-store",
                    Kind = "ClusterSecretStore",
                },
                Target = new ExternalSecretSpecTargetArgs
                {
                    Name = "warpstream-agent-apikey",
                },
                DataFrom = new ExternalSecretSpecDataFromArgs
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs
                    {
                        Key = "id:ae402e70-87ee-435a-8ecc-f6c91c57ae9c",
                        Version = "latest_enabled",
                    }
                }
            }
        }, new()
        {
            Provider = provider,
            Parent = this,
        });
    }
}
