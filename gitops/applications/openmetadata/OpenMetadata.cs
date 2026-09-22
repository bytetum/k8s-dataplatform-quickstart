using System.Collections.Generic;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.openmetadata;

/// <summary>
/// Materializes the OpenMetadata dependency credentials from the local source
/// store.  The dependency chart and Airflow consume these existing Secrets;
/// no database or Airflow password is committed in values files.
/// </summary>
internal sealed class OpenMetadataDatabase : ComponentResource
{
    public OpenMetadataDatabase(string manifestsRoot)
        : base("openmetadata-database", "openmetadata-database")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/openmetadata-dependencies",
        }, new CustomResourceOptions
        {
            Parent = this,
        });

        _ = new ExternalSecret("mysql-secrets", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "mysql-secrets",
                Namespace = Constants.OpenMetadataNamespace,
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
                    Name = "mysql-secrets",
                    Template = new ExternalSecretSpecTargetTemplateArgs
                    {
                        Data = new Dictionary<string, string>
                        {
                            ["mysql-root-password"] = "{{ index . \"root-password\" }}",
                            ["mysql-password"] = "{{ .password }}",
                            ["openmetadata-mysql-password"] = "{{ .password }}",
                        },
                    },
                },
                DataFrom = new ExternalSecretSpecDataFromArgs
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs
                    {
                        Key = SecretSources.OpenMetadataDatabaseCredentials,
                    },
                },
            },
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = this,
        });

        _ = new ExternalSecret("airflow-secrets", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "airflow-secrets",
                Namespace = Constants.OpenMetadataNamespace,
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
                    Name = "airflow-secrets",
                    Template = new ExternalSecretSpecTargetTemplateArgs
                    {
                        Data = new Dictionary<string, string>
                        {
                            ["openmetadata-airflow-password"] = "{{ .password }}",
                            ["AIRFLOW_ADMIN_PASSWORD"] = "{{ .password }}",
                        },
                    },
                },
                DataFrom = new ExternalSecretSpecDataFromArgs
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs
                    {
                        Key = SecretSources.OpenMetadataAirflowCredentials,
                    },
                },
            },
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = this,
        });

        _ = new ExternalSecret("airflow-metadata-secret", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "airflow-metadata-secret",
                Namespace = Constants.OpenMetadataNamespace,
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
                    Name = "airflow-metadata-secret",
                    Template = new ExternalSecretSpecTargetTemplateArgs
                    {
                        Data = new Dictionary<string, string>
                        {
                            ["connection"] = "{{ .connection }}",
                        },
                    },
                },
                DataFrom = new ExternalSecretSpecDataFromArgs
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs
                    {
                        Key = SecretSources.OpenMetadataAirflowCredentials,
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
