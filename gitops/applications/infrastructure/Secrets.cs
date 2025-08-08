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
                    //MARK: change
                    Fake = new ClusterSecretStoreSpecProviderFakeArgs
                    {
                        Data = new InputList<ClusterSecretStoreProviderDataFakeArgs>()
                        {
                            new ClusterSecretStoreProviderDataFakeArgs
                            {
                                Key = "id:c2f85be8-7fd0-402d-8229-6de987bcbbb4",
                                Value = "{\"SCALEWAY_ACCESS_KEY\": \"ACCESS_KEY\", \"SCALEWAY_SECRET_KEY\": \"SECRET_KEY\", \"SCALEWAY_ROLE_ARN\": \"ROLE_ID\", \"SCALEWAY_REGION\": \"SCALEWAY_REGION\"}",
                                Version = "latest_enabled"
                            },
                            new ClusterSecretStoreProviderDataFakeArgs
                            {
                                //wait for PE to generate
                                Key = "id:842cb98e-9786-4cc6-9af7-424f9278d802",
                                Value = "{\"public.pem\": \"value\", \"private.pem\": \"value\"}",
                                Version = "latest_enabled",
                            },
                            new ClusterSecretStoreProviderDataFakeArgs
                            {
                                Key = "id:842cb98e-9786-4cc6-9af7-424f9278d808",
                                Value = "{\"db-address\": \"postgres-service\", \"username\": \"value\", \"password\": \"value\"}",
                                Version = "latest_enabled",
                            },
                            new ClusterSecretStoreProviderDataFakeArgs
                            {
                                Key = "id:polaris-root-password",
                                Value = "{\"polaris-root-password\": \"s3cr3t\"}",
                                Version = "latest_enabled",
                            }
                        }
                    }
                    //MARK: endchange
                }
            }
        }, new()
        {
            Provider = provider,
            Parent = this,
        });
    }
}

