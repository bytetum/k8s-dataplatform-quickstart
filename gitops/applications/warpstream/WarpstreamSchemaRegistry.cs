using Pulumi.Crds.ExternalSecrets;

namespace applications.warpstream;

internal class WarpstreamSchemaRegistry : ComponentResource
{
    public WarpstreamSchemaRegistry(string manifestsRoot)
        : base("warpstream-schema-registry", "warpstream-schema-registry")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/warpstream-schema-registry"
        }, new()
        {
            Parent = this,
        });

        var schemaRegistrySecret = new ExternalSecret("schema-registry-secret", new()
        {
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "warpstream-schema-registry-secrets",
                Namespace = Constants.WarpStreamNamespace,
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
                    Name = "warpstream-schema-registry-secrets",
                },
                DataFrom = new ExternalSecretSpecDataFromArgs
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs
                    {
                        Key = SecretSources.WarpstreamSchemaRegistrySecrets,
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
