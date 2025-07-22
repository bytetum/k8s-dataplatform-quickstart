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
                    Scaleway =
                    new ClusterSecretStoreSpecProviderScalewayArgs
                    {
                        Region = "nl-ams",
                        ProjectId = config.Require("project_id"),
                        AccessKey = new ClusterSecretStoreSpecProviderScalewayAceessKeyArgs
                        {
                            SecretRef = new SecretRefArgs
                            {
                                Name = "secret-manager",
                                Key = "access-key",
                                Namespace = "external-secrets"
                            }
                        },
                        SecretKey = new ClusterSecretStoreSpecProviderScalewaySecretKeyArgs
                        {
                            SecretRef = new SecretRefArgs
                            {
                                Name = "secret-manager",
                                Key = "secret-key",
                                Namespace = "external-secrets"
                            }
                        }
                    }
                },
                Conditions = new ClusterSecretStoreSpecConditionsArgs
                {
                    Namespaces = ["test"]
                }
            }
        }, new()
        {
            Provider = provider,
            Parent = this,
        });
    }
}

