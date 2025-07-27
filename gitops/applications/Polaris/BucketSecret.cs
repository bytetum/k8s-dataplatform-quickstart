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
                        Key = "id: c2f85be8-7fd0-402d-8229-6de987bcbbb4"
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