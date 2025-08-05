using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Types.Inputs.Batch.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Job = Pulumi.Kubernetes.Batch.V1.Job;

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
                Template = new PodTemplateSpecArgs
                {
                    Spec = new PodSpecArgs
                    {
                        RestartPolicy = "OnFailure",
                        InitContainers = new ContainerArgs
                        {
                            Name = "wait-for-polaris-api",
                            Image = "alpine/curl",
                            Command = new InputList<string>
                            {
                                "sh",
                                "-c",
                                """
                                max_attempts=15
                                attempt=1
                                while [ $attempt -le $max_attempts ]; do
                                  echo "Attempt $attempt: Checking Polaris API health..."
                                  if curl --fail --silent --output /dev/null http://polaris-mgmt:8182/q/health/live; then
                                    echo "Polaris API is healthy!"
                                    exit 0
                                  fi
                                  echo "Waiting for Polaris API... (attempt $attempt of $max_attempts)"
                                  attempt=$((attempt + 1))
                                  sleep 5
                                done
                                echo "Polaris API did not become healthy after $max_attempts attempts."
                                exit 1
                                """.Replace("\r\n", "\n")
                            }
                        },
                        Containers = new ContainerArgs
                        {
                            Name = "create-catalog",
                            Image = "alpine/curl",
                            Env = new InputList<EnvVarArgs>
                            {
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
                                apk add --no-cache jq curl

                                token=$(curl -s http://polaris:8181/api/catalog/v1/oauth/tokens \
                                    --user ${CLIENT_ID}:${CLIENT_SECRET} \
                                    -d grant_type=client_credentials \
                                    -d scope=PRINCIPAL_ROLE:ALL | sed -n 's/.*\"access_token\":\"\([^\"]*\)\".*/\1/p')
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
                                echo "STORAGE_LOCATION is set to '${STORAGE_LOCATION}'"
                                if [[ "${STORAGE_LOCATION}" == s3* ]]; then
                                STORAGE_TYPE="S3"
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
                                fi

                                echo
                                echo Creating a catalog named quickstart_catalog...

                                PAYLOAD='{
                                "catalog": {
                                "name": "quickstart_catalog",
                                "type": "INTERNAL",
                                "readOnly": false,
                                "properties": {
                                "default-base-location": "'"$STORAGE_LOCATION"'"
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
                                echo Done.
                                """.Replace("\r\n", "\n")
                                // This is the key part: build the script from an array of strings.
                                
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


        var bucketSecret = new ExternalSecret("bucket-secret", new()
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