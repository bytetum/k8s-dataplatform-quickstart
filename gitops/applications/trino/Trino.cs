using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.trino;

public class Trino : ComponentResource
{
    public Trino(string manifestsRoot) : base("trino", "trino")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/trino",
        }, new CustomResourceOptions
        {
            Parent = this,
        });

        var resourceOptions = new CustomResourceOptions
        {
            Parent = this,
            Provider = provider,
        };

        _ = CreateExternalSecret(
            "trino-iceberg-bucket-credentials",
            "iceberg-bucket-credentials",
            SecretSources.IcebergBucketCredentials,
            resourceOptions);
        _ = CreateExternalSecret(
            "trino-polaris-credentials",
            "trino-polaris-credentials",
            SecretSources.PolarisRootPassword,
            resourceOptions);
    }

    private static ExternalSecret CreateExternalSecret(
        string resourceName,
        string targetName,
        string sourceKey,
        CustomResourceOptions resourceOptions) =>
        new(resourceName, new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = targetName,
                Namespace = Constants.TrinoNamespace,
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
                    Name = targetName,
                },
                DataFrom = new ExternalSecretSpecDataFromArgs
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs
                    {
                        Key = sourceKey,
                    },
                },
            },
        }, resourceOptions);
}
