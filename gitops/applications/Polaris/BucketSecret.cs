using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.Polaris.external_secrets;

using Pulumi.Crds.ExternalSecrets;

public class BucketSecret : ComponentResource
{
    public BucketSecret(string manifestsRoot) : base("secret", "secret")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/polaris"
        }, new CustomResourceOptions
        {
            Parent = this
        });

        var bucketSecret = new ExternalSecret("bucket-secret", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "aws-secret",
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
                    Name = "aws-secret-local"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = "aws-secret-local"
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