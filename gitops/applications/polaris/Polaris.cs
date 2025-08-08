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
                Namespace = "polaris",
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
                            Env = new InputList<EnvVarArgs>
                            {
                                new EnvVarArgs
                                {
                                    Name = "CATALOG_NAME",
                                    Value = "ao_catalog"
                                },
                                new EnvVarArgs
                                {
                                    Name = "STORAGE_LOCATION",
                                    Value = "s3://k8s-essence/"
                                },
                                new EnvVarArgs
                                {
                                    Name = "CLIENT_ID",
                                    Value = "root"
                                },
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
                                new EnvVarArgs
                                {
                                    Name = "AWS_ROLE_ARN",
                                    ValueFrom = new EnvVarSourceArgs
                                    {
                                        SecretKeyRef = new SecretKeySelectorArgs
                                        {
                                            Name = "iceberg-bucket-credentials",
                                            Key = "SCALEWAY_ROLE_ARN"
                                        }
                                    }
                                }
                            },
                            Command = new InputList<string>
                            {
                                "sh",
                                "-c",
                                """
                                set -e

                                apk add --no-cache jq

                                token=$(curl -s http://polaris:8181/api/catalog/v1/oauth/tokens \
                                --user ${CLIENT_ID}:${CLIENT_SECRET} \
                                -d grant_type=client_credentials \
                                -d scope=PRINCIPAL_ROLE:ALL | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')

                                if [ -z "${token}" ]; then
                                echo "Failed to obtain access token."
                                exit 1
                                fi

                                echo
                                echo "Obtained access token: ${token}"

                                STORAGE_TYPE="FILE"
                                if [ -z "${STORAGE_LOCATION}" ]; then
                                    echo "STORAGE_LOCATION is not set, using FILE storage type"
                                    STORAGE_LOCATION="file:///var/tmp/quickstart_catalog/"
                                else
                                    echo "STORAGE_LOCATION is set to '$STORAGE_LOCATION'"
                                    if [[ "$STORAGE_LOCATION" == s3* ]]; then
                                        STORAGE_TYPE="S3"
                                    elif [[ "$STORAGE_LOCATION" == gs* ]]; then
                                        STORAGE_TYPE="GCS"
                                    else
                                        STORAGE_TYPE="AZURE"
                                    fi
                                    echo "Using StorageType: $STORAGE_TYPE"
                                fi

                                STORAGE_CONFIG_INFO="{\"storageType\": \"$STORAGE_TYPE\", \"allowedLocations\": [\"$STORAGE_LOCATION\"]}"

                                if [[ "$STORAGE_TYPE" == "S3" ]]; then
                                    if [ -n "${AWS_ROLE_ARN}" ]; then
                                        STORAGE_CONFIG_INFO=$(echo "$STORAGE_CONFIG_INFO" | jq --arg roleArn "$AWS_ROLE_ARN" '. + {roleArn: $roleArn}')
                                    else
                                        echo "Warning: AWS_ROLE_ARN not set for S3 storage"
                                    fi
                                elif [[ "$STORAGE_TYPE" == "AZURE" ]]; then
                                    if [ -n "${AZURE_TENANT_ID}" ]; then
                                        STORAGE_CONFIG_INFO=$(echo "$STORAGE_CONFIG_INFO" | jq --arg tenantId "$AZURE_TENANT_ID" '. + {tenantId: $tenantId}')
                                    fi
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

                                curl -s -f -H "Authorization: Bearer ${token}" \
                                -H 'Accept: application/json' \
                                -H 'Content-Type: application/json' \
                                http://polaris:8181/api/management/v1/catalogs \
                                -d "$PAYLOAD" -v

                                echo
                                echo Done.
                                """.Replace("\r\n", "\n")
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
                Namespace = "polaris",
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = "secret-store",
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
                        Key = "id:c2f85be8-7fd0-402d-8229-6de987bcbbb4"
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
                Namespace = "polaris",
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = "secret-store",
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
                        Key = "id:842cb98e-9786-4cc6-9af7-424f9278d802"
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
                Namespace = "polaris",
            },
            Spec = new ExternalSecretSpecArgs
            {
                SecretStoreRef = new ExternalSecretSpecSecretStoreRefArgs()
                {
                    Name = "secret-store",
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
                        Key = "id:polaris-root-password"
                    }
                }
            }
        }, new()
        {
            Parent = this,
            Provider = provider
        });
    }
}