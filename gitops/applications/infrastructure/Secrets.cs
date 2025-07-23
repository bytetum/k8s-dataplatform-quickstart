using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.infrastructure;

internal class Secrets : ComponentResource
{
    public Secrets(string manifestsRoot)
        : base("secrets", "secrets")
    {
        var config = new Config("scaleway");

        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/secrets"
        }, new CustomResourceOptions
        {
            Parent = this
        });

        var secretStore = new ClusterSecretStore("secret-store", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "secret-store",
                Namespace = "external-secrets",
            },
            Spec = new ClusterSecretStoreSpecArgs
            {
                Provider = new ClusterSecretStoreSpecProviderArgs
                {
                    Fake = new ClusterSecretStoreSpecProviderFakeArgs
                    {
                        Data = new InputList<ClusterSecretStoreProviderDataFakeArgs>()
                        {
                            new ClusterSecretStoreProviderDataFakeArgs
                            {
                                Key = "bucket/access-key",
                                Value = "local-app-user"
                            },
                            new ClusterSecretStoreProviderDataFakeArgs
                            {
                                Key = "bucket/secret-key",
                                Value = "a-very-secure-password",
                            }
                        }
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

