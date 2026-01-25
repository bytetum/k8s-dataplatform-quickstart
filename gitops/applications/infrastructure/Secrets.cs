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
                                Value = "{\"AZURE_STORAGE_ACCOUNT_NAME\": \"PLACEHOLDER_STORAGE_ACCOUNT\", \"AZURE_TENANT_ID\": \"PLACEHOLDER_TENANT_ID\", \"AZURE_CLIENT_ID\": \"PLACEHOLDER_CLIENT_ID\"}",
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
                                Value = "{\"AZURE_STORAGE_ACCOUNT_NAME\": \"PLACEHOLDER_STORAGE_ACCOUNT\", \"AZURE_TENANT_ID\": \"PLACEHOLDER_TENANT_ID\", \"AZURE_CLIENT_ID\": \"PLACEHOLDER_CLIENT_ID\"}",
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
							new ClusterSecretStoreProviderDataFakeArgs
							{
								Key = "id:schema-registry-credentials",
								Value = "{\"username\": \"ccun_291350ada8541780bdbc5663f2d22855a4da5bf905a576bac6c8dfa95c89db71\", \"password\": \"ccp_956975877bc5eeb62ce21d18c49d320a3d128cb9d0c81278999a742f6272090e\"}",
								Version = "latest_enabled"
							},
							new ClusterSecretStoreProviderDataFakeArgs
							{
								Key = "id:flink-azure-credentials-secret",
								Value = "{\"AZURE_STORAGE_ACCOUNT_NAME\": \"PLACEHOLDER_STORAGE_ACCOUNT\", \"AZURE_TENANT_ID\": \"PLACEHOLDER_TENANT_ID\", \"AZURE_CLIENT_ID\": \"PLACEHOLDER_CLIENT_ID\"}",
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

