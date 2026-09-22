using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Batch.V1;
using Pulumi.Kubernetes.Types.Inputs.Batch.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.polaris;

public class Polaris : ComponentResource
{
    public Polaris(string manifestsRoot) : base("polaris", "polaris")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/polaris",
        }, new CustomResourceOptions
        {
            Parent = this
        });

        var polarisPostSyncHook = new Job("polaris-post-sync-hook", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "polaris-catalog-creator",
                Namespace = Constants.PolarisNamespace,
                Annotations = new InputMap<string>()
                {
                    { "argocd.argoproj.io/hook", "PostSync" },
                    { "argocd.argoproj.io/hook-delete-policy", "HookSucceeded" },
                }
            },
            Spec = new JobSpecArgs
            {
                BackoffLimit = 2,
                Template = new PodTemplateSpecArgs
                {
                    Spec = new PodSpecArgs
                    {
                        RestartPolicy = "OnFailure",
                        Containers = new ContainerArgs
                        {
                            Name = "create-catalog",
                            Image = "alpine/curl",
                            Env = CatalogCreatorEnvironment(),
                            Command = new InputList<string>
                            {
                                "sh",
                                "-c",
                                CreateCatalogScript()
                            }
                        }
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });


        var icebergBucketCredentials = new ExternalSecret("iceberg-bucket-credentials", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "iceberg-bucket-credentials",
                Namespace = Constants.PolarisNamespace,
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = SecretSources.StoreName,
                    Kind = "ClusterSecretStore"
                },
                Target = new ExternalSecretSpecTargetArgs()
                {
                    Name = "iceberg-bucket-credentials"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = SecretSources.IcebergBucketCredentials
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        var polarisKeyPair = new ExternalSecret("polaris-key-pair", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "polaris-key-pair",
                Namespace = Constants.PolarisNamespace,
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = SecretSources.StoreName,
                    Kind = "ClusterSecretStore"
                },
                Target = new ExternalSecretSpecTargetArgs()
                {
                    Name = "polaris-key-pair"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = SecretSources.PolarisKeyPair
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });

        var polarisRootPassword = new ExternalSecret("polaris-root-password", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "polaris-root-password",
                Namespace = Constants.PolarisNamespace,
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = SecretSources.StoreName,
                    Kind = "ClusterSecretStore"
                },
                Target = new ExternalSecretSpecTargetArgs()
                {
                    Name = "polaris-root-password"
                },
                DataFrom = new ExternalSecretSpecDataFromArgs()
                {
                    Extract = new ExternalSecretSpecDataFromExtractArgs()
                    {
                        Key = SecretSources.PolarisRootPassword
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });
    }

    private static InputList<EnvVarArgs> CatalogCreatorEnvironment()
    {
        var env = new InputList<EnvVarArgs>
        {
            new EnvVarArgs { Name = "CATALOG_NAME", Value = Constants.PolarisCatalog },
            new EnvVarArgs { Name = "STORAGE_LOCATION", Value = $"{Constants.IcebergBucketPath}/" },
            new EnvVarArgs { Name = "CLIENT_ID", Value = "root" },
            new EnvVarArgs
            {
                Name = "CLIENT_SECRET",
                ValueFrom = new EnvVarSourceArgs
                {
                    SecretKeyRef = new SecretKeySelectorArgs
                    {
                        Name = "polaris-root-password",
                        Key = "polaris-root-password"
                    }
                }
            },
        };
        if (Constants.IsKindLocal)
        {
            env.Add(new EnvVarArgs { Name = "ALLOW_CATALOG_CREATE", Value = "true" });
        }
        env.Add(new EnvVarArgs
        {
            Name = "AWS_ROLE_ARN",
            ValueFrom = new EnvVarSourceArgs
            {
                SecretKeyRef = new SecretKeySelectorArgs
                {
                    Name = "iceberg-bucket-credentials",
                    Key = "AWS_ROLE_ARN"
                }
            }
        });
        return env;
    }

    private static string CreateCatalogScript()
    {
        var script = Constants.IsKindLocal
            ? """
                                set -e
                                apk add --no-cache jq

                                token=$(curl -s http://polaris:8181/api/catalog/v1/oauth/tokens \
                                    --user ${CLIENT_ID}:${CLIENT_SECRET} \
                                    -d grant_type=client_credentials \
                                    -d scope=PRINCIPAL_ROLE:ALL | jq -r '.access_token')
                                
                                if [ -z "${token}" ]; then
                                    echo "Failed to obtain access token."
                                    exit 1
                                fi
                                
                                echo "Obtained Polaris access token."

                                case "$STORAGE_LOCATION" in
                                    s3*) STORAGE_TYPE="S3" ;;
                                    *)
                                        echo "Error: Only S3 storage is supported. STORAGE_LOCATION must start with 's3'."
                                        exit 1
                                        ;;
                                esac
                                
                                echo "Using StorageType: $STORAGE_TYPE"

                                STORAGE_CONFIG_INFO="{\"storageType\": \"$STORAGE_TYPE\", \"allowedLocations\": [\"$STORAGE_LOCATION\"]}"
                                if [ -n "${AWS_ROLE_ARN}" ]; then
                                    STORAGE_CONFIG_INFO=$(echo "$STORAGE_CONFIG_INFO" | jq --arg roleArn "$AWS_ROLE_ARN" '. + {roleArn: $roleArn}')
                                else
                                    echo "Warning: AWS_ROLE_ARN not set for S3 storage"
                                fi

                                response=$(curl -s -w "\n%{http_code}" \
                                    -H "Authorization: Bearer ${token}" \
                                    -H "Accept: application/json" \
                                    -H "Content-Type: application/json" \
                                    "http://polaris:8181/api/management/v1/catalogs/${CATALOG_NAME}")

                                status_code=$(echo "$response" | tail -n1)

                                if [ "$status_code" -eq 200 ]; then
                                    echo "Catalog already exists, skipping creation..."
                                    exit 0
                                elif [ "$status_code" -eq 404 ]; then
                                    if [ "${ALLOW_CATALOG_CREATE}" != "true" ]; then
                                        echo "Catalog is missing; refusing to create a replacement during migration."
                                        exit 1
                                    fi
                                    echo "Catalog does not exist and explicit creation is enabled."
                                else
                                    echo "Unexpected Polaris catalog lookup response (HTTP ${status_code})."
                                    exit 1
                                fi

                                echo
                                echo Creating a catalog named $CATALOG_NAME...

                                PAYLOAD='{
                                    "catalog": {
                                        "name": "'$CATALOG_NAME'",
                                        "type": "INTERNAL",
                                        "readOnly": false,
                                        "properties": {
                                            "default-base-location": "'$STORAGE_LOCATION'"
                                        },
                                        "storageConfigInfo": '$STORAGE_CONFIG_INFO'
                                    }
                                }'

                                curl -sS -f -o /dev/null \
                                    -H "Authorization: Bearer ${token}" \
                                    -H 'Accept: application/json' \
                                    -H 'Content-Type: application/json' \
                                    http://polaris:8181/api/management/v1/catalogs \
                                    -d "$PAYLOAD"
                                
                                echo
                                echo "Granting CATALOG_MANAGE_CONTENT privilege..."
                                curl -sS -f -o /dev/null \
                                    -H "Authorization: Bearer ${token}" \
                                    -H 'Content-Type: application/json' \
                                    -X PUT \
                                    http://polaris:8181/api/management/v1/catalogs/${CATALOG_NAME}/catalog-roles/catalog_admin/grants \
                                    -d '{"type":"catalog", "privilege":"CATALOG_MANAGE_CONTENT"}'
                                
                                echo
                                echo Done.
                                """
            : """
                                set -e
                                apk add --no-cache jq

                                token=$(curl -s http://polaris:8181/api/catalog/v1/oauth/tokens \
                                    --user ${CLIENT_ID}:${CLIENT_SECRET} \
                                    -d grant_type=client_credentials \
                                    -d scope=PRINCIPAL_ROLE:ALL | jq -r '.access_token')
                                
                                if [ -z "${token}" ]; then
                                    echo "Failed to obtain access token."
                                    exit 1
                                fi
                                
                                echo
                                echo "Obtained access token: ${token}"

                                if [[ "$STORAGE_LOCATION" == s3* ]]; then
                                    STORAGE_TYPE="S3"
                                else
                                    echo "Error: Only S3 storage is supported. STORAGE_LOCATION must start with 's3'."
                                    exit 1
                                fi
                                
                                echo "Using StorageType: $STORAGE_TYPE"

                                STORAGE_CONFIG_INFO="{\"storageType\": \"$STORAGE_TYPE\", \"allowedLocations\": [\"$STORAGE_LOCATION\"]}"
                                if [ -n "${AWS_ROLE_ARN}" ]; then
                                    STORAGE_CONFIG_INFO=$(echo "$STORAGE_CONFIG_INFO" | jq --arg roleArn "$AWS_ROLE_ARN" '. + {roleArn: $roleArn}')
                                else
                                    echo "Warning: AWS_ROLE_ARN not set for S3 storage"
                                fi

                                response=$(curl -s -w "\n%{http_code}" \
                                    -H "Authorization: Bearer ${token}" \
                                    -H "Accept: application/json" \
                                    -H "Content-Type: application/json" \
                                    "http://polaris:8181/api/management/v1/catalogs/${CATALOG_NAME}")

                                status_code=$(echo "$response" | tail -n1)

                                if [ "$status_code" -eq 200 ]; then
                                    echo "Catalog already exists, skipping creation..."
                                    exit 0
                                elif [ "$status_code" -eq 404 ]; then
                                    echo "Catalog does not exist, proceeding..."
                                else
                                    echo "$response"
                                    exit 1
                                fi

                                echo
                                echo Creating a catalog named $CATALOG_NAME...

                                PAYLOAD='{
                                    "catalog": {
                                        "name": "'$CATALOG_NAME'",
                                        "type": "INTERNAL",
                                        "readOnly": false,
                                        "properties": {
                                            "default-base-location": "'$STORAGE_LOCATION'"
                                        },
                                        "storageConfigInfo": '$STORAGE_CONFIG_INFO'
                                    }
                                }'

                                echo $PAYLOAD

                                curl -s -H "Authorization: Bearer ${token}" \
                                    -H 'Accept: application/json' \
                                    -H 'Content-Type: application/json' \
                                    http://polaris:8181/api/management/v1/catalogs \
                                    -d "$PAYLOAD" -v
                                
                                echo
                                echo "Granting CATALOG_MANAGE_CONTENT privilege..."
                                curl -s -H "Authorization: Bearer ${token}" \
                                    -H 'Content-Type: application/json' \
                                    -X PUT \
                                    http://polaris:8181/api/management/v1/catalogs/${CATALOG_NAME}/catalog-roles/catalog_admin/grants \
                                    -d '{"type":"catalog", "privilege":"CATALOG_MANAGE_CONTENT"}' -v
                                
                                echo
                                echo Done.
                                """;
        return script.Replace("\r\n", "\n");
    }
}
