using System.Collections.Generic;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.marquez;

/// <summary>
/// Provides Marquez's fresh local PostgreSQL credentials from the existing
/// local source store.  The chart consumes an existing Secret, so no password
/// literal is committed in the kind-local values or generated manifests.
/// </summary>
internal sealed class MarquezDatabase : ComponentResource
{
    public MarquezDatabase(string manifestsRoot)
        : base("marquez-database", "marquez-database")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/marquez",
        }, new CustomResourceOptions
        {
            Parent = this,
        });

        _ = new ExternalSecret("marquez-postgres-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "marquez-postgres-credentials",
                Namespace = Constants.MarquezNamespace,
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
                    Name = "marquez-postgres-credentials",
                    Template = new ExternalSecretSpecTargetTemplateArgs
                    {
                        Data = new Dictionary<string, string>
                        {
                            ["postgres-password"] = "{{ .password }}",
                            ["password"] = "{{ .password }}",
                            ["replication-password"] = "{{ .password }}",
                        },
                    },
                },
                DataFrom = new ExternalSecretSpecDataFromArgs
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs
                    {
                        Key = SecretSources.MarquezDatabaseCredentials,
                    },
                },
            },
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = this,
        });
    }
}
