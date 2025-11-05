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
                                Value = "{\"AWS_ACCESS_KEY\": \"AWS_ACCESS_KEY\", \"AWS_SECRET_KEY\": \"AWS_SECRET_KEY\", \"AWS_ROLE_ARN\": \"AWS_ROLE_ARN\", \"AWS_REGION\": \"AWS_REGION\"}",
                                Version = "latest_enabled"
                            },
                            new ClusterSecretStoreProviderDataFakeArgs
                            {
                                //wait for PE to generate
                                Key = "id:842cb98e-9786-4cc6-9af7-424f9278d802",
                                Value = "{\"public.pem\": \"public.pem\", \"private.pem\": \"private.pem\"}",
                                Version = "latest_enabled",
                            },
                            new ClusterSecretStoreProviderDataFakeArgs
                            {
                                Key = "id:842cb98e-9786-4cc6-9af7-424f9278d808",
                                Value = "{\"db-address\": \"db-address\", \"username\": \"username\", \"password\": \"password\"}",
                                Version = "latest_enabled",
                            },
                            new ClusterSecretStoreProviderDataFakeArgs
                            {
                                Key = "id:polaris-root-password",
                                Value = "{\"polaris-root-password\": \"polaris-root-password\"}",
                                Version = "latest_enabled",
                            }
							,
							new ClusterSecretStoreProviderDataFakeArgs
							{
								Key = "id:827b85c8-babe-4a43-8af2-dce1dd530081",
                                Value = "{\"SCALEWAY_ACCESS_KEY\": \"ACCESS_KEY\", \"SCALEWAY_SECRET_KEY\": \"SECRET_KEY\"}",
								Version = "latest_enabled"
							},
							new ClusterSecretStoreProviderDataFakeArgs
							{
								Key = "id:ae402e70-87ee-435a-8ecc-f6c91c57ae9c",
								Value = "{\"agent_key\": \"agent_key\"}",
								Version = "latest_enabled"
							},
							new ClusterSecretStoreProviderDataFakeArgs
							{
								Key = "id:flink-warpstream-credentials-secret",
								Value = "{\"USERNAME\": \"USERNAME\", \"PASSWORD\": \"PASSWORD\"}",
								Version = "latest_enabled"
							},
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

