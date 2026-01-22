using System;
using System.Security.Cryptography;
using System.Text;
using Pulumi;
using Pulumi.Kubernetes.Batch.V1;
using Pulumi.Kubernetes.Types.Inputs.Batch.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

/// <summary>
/// Builder for creating a Kubernetes Job that validates schema existence in Schema Registry.
/// Used as an ArgoCD PreSync hook - the sync will not proceed until this job succeeds.
/// This removes the need for sync wave configuration as ArgoCD handles ordering natively.
/// </summary>
public class SchemaValidationJobBuilder
{
    private Pulumi.Kubernetes.Provider _provider = null!;
    private Resource _parent = null!;
    private string _jobName = "";
    private string _namespace = "kafka-connect";
    private string _schemaSubject = "";
    private string _schemaRegistryUrl = "http://warpstream-schema-registry-warpstream-agent.warpstream.svc.cluster.local:9094";
    private int _timeoutSeconds = 300;
    private int _retryIntervalSeconds = 5;
    private int _backoffLimit = 3;

    /// <summary>
    /// Set the Kubernetes provider for YAML generation.
    /// </summary>
    public SchemaValidationJobBuilder WithProvider(Pulumi.Kubernetes.Provider provider)
    {
        _provider = provider;
        return this;
    }

    /// <summary>
    /// Set the parent resource for Pulumi resource hierarchy.
    /// </summary>
    public SchemaValidationJobBuilder WithParent(Resource parent)
    {
        _parent = parent;
        return this;
    }

    /// <summary>
    /// Set the job name (required). Usually matches the connector or Flink deployment name.
    /// </summary>
    public SchemaValidationJobBuilder WithJobName(string jobName)
    {
        _jobName = jobName;
        return this;
    }

    /// <summary>
    /// Set the namespace for the job (default: kafka-connect)
    /// </summary>
    public SchemaValidationJobBuilder WithNamespace(string ns)
    {
        _namespace = ns;
        return this;
    }

    /// <summary>
    /// Set the schema subject to validate (required).
    /// Example: "silver.m3.cidmas-value" for Avro value schema
    /// </summary>
    public SchemaValidationJobBuilder WithSchemaSubject(string schemaSubject)
    {
        _schemaSubject = schemaSubject;
        return this;
    }

    /// <summary>
    /// Set the Schema Registry URL
    /// </summary>
    public SchemaValidationJobBuilder WithSchemaRegistryUrl(string url)
    {
        _schemaRegistryUrl = url;
        return this;
    }

    /// <summary>
    /// Set the timeout in seconds for waiting for schema (default: 300)
    /// </summary>
    public SchemaValidationJobBuilder WithTimeout(int timeoutSeconds)
    {
        _timeoutSeconds = timeoutSeconds;
        return this;
    }

    /// <summary>
    /// Set the retry interval in seconds (default: 5)
    /// </summary>
    public SchemaValidationJobBuilder WithRetryInterval(int retryIntervalSeconds)
    {
        _retryIntervalSeconds = retryIntervalSeconds;
        return this;
    }

    /// <summary>
    /// Set the Kubernetes Job backoff limit (default: 3)
    /// </summary>
    public SchemaValidationJobBuilder WithBackoffLimit(int backoffLimit)
    {
        _backoffLimit = backoffLimit;
        return this;
    }

    /// <summary>
    /// Build and create the validation Job resource as an ArgoCD PreSync hook.
    /// </summary>
    public Job Build()
    {
        // Shell script that polls Schema Registry until schema exists or timeout
        // Using $$""" so that {{var}} is C# interpolation, and {single} is literal shell brace
        var validationScript = $$"""
            #!/bin/sh
            set -e

            SCHEMA_REGISTRY_URL="{{_schemaRegistryUrl}}"
            SUBJECT="{{_schemaSubject}}"
            TIMEOUT={{_timeoutSeconds}}
            INTERVAL={{_retryIntervalSeconds}}

            echo "Checking schema subject: $SUBJECT"
            echo "Schema Registry: $SCHEMA_REGISTRY_URL"
            echo "Timeout: ${TIMEOUT}s, Retry interval: ${INTERVAL}s"

            elapsed=0
            while [ $elapsed -lt $TIMEOUT ]; do
                response=$(curl -s -o /dev/null -w "%{http_code}" \
                    -u "$SCHEMA_REGISTRY_USERNAME:$SCHEMA_REGISTRY_PASSWORD" \
                    "$SCHEMA_REGISTRY_URL/subjects/$SUBJECT/versions/latest")

                if [ "$response" = "200" ]; then
                    echo "Schema found for subject: $SUBJECT"
                    schema_info=$(curl -s \
                        -u "$SCHEMA_REGISTRY_USERNAME:$SCHEMA_REGISTRY_PASSWORD" \
                        "$SCHEMA_REGISTRY_URL/subjects/$SUBJECT/versions/latest")
                    echo "Schema info: $schema_info"
                    exit 0
                fi

                echo "Schema not found (HTTP $response), retrying in ${INTERVAL}s... ($elapsed/${TIMEOUT}s)"
                sleep $INTERVAL
                elapsed=$((elapsed + INTERVAL))
            done

            echo "Timeout waiting for schema subject: $SUBJECT"
            exit 1
            """.Replace("\r\n", "\n");

        return new Job($"schema-check-{_jobName}", new JobArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = $"schema-check-{_jobName}",
                Namespace = _namespace,
                Labels = new InputMap<string>
                {
                    { "app", "schema-check" },
                    { "target", _jobName }
                },
                Annotations = new InputMap<string>
                {
                    // PreSync hook: runs before the main sync, blocks until successful
                    { "argocd.argoproj.io/hook", "PreSync" },
                    // Delete previous job before creating new one (allows re-runs)
                    { "argocd.argoproj.io/hook-delete-policy", "BeforeHookCreation" },
                    { "config-hash", ComputeConfigHash() }
                }
            },
            Spec = new JobSpecArgs
            {
                BackoffLimit = _backoffLimit,
                TtlSecondsAfterFinished = 300,
                Template = new PodTemplateSpecArgs
                {
                    Spec = new PodSpecArgs
                    {
                        RestartPolicy = "Never",
                        Containers = new InputList<ContainerArgs>
                        {
                            new ContainerArgs
                            {
                                Name = "schema-check",
                                Image = "curlimages/curl:8.5.0",
                                Command = new InputList<string> { "sh", "-c", validationScript },
                                Env = new InputList<EnvVarArgs>
                                {
                                    new EnvVarArgs
                                    {
                                        Name = "SCHEMA_REGISTRY_USERNAME",
                                        ValueFrom = new EnvVarSourceArgs
                                        {
                                            SecretKeyRef = new SecretKeySelectorArgs
                                            {
                                                Name = "schema-registry-credentials",
                                                Key = "username"
                                            }
                                        }
                                    },
                                    new EnvVarArgs
                                    {
                                        Name = "SCHEMA_REGISTRY_PASSWORD",
                                        ValueFrom = new EnvVarSourceArgs
                                        {
                                            SecretKeyRef = new SecretKeySelectorArgs
                                            {
                                                Name = "schema-registry-credentials",
                                                Key = "password"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }, new CustomResourceOptions
        {
            Provider = _provider,
            Parent = _parent
        });
    }

    private string ComputeConfigHash()
    {
        var configString = $"{_jobName}|{_schemaSubject}|{_schemaRegistryUrl}|{_timeoutSeconds}|{_retryIntervalSeconds}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(configString));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
