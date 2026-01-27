using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pulumi;
using Pulumi.Kubernetes.Batch.V1;
using Pulumi.Kubernetes.Types.Inputs.Batch.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

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
    private NamingConventionHelper.SchemaCompatibility? _schemaCompatibility;
    private bool _autoSetCompatibility = true;

    public SchemaValidationJobBuilder WithProvider(Pulumi.Kubernetes.Provider provider)
    {
        _provider = provider;
        return this;
    }

    public SchemaValidationJobBuilder WithParent(Resource parent)
    {
        _parent = parent;
        return this;
    }

    public SchemaValidationJobBuilder WithJobName(string jobName)
    {
        // Sanitize for Kubernetes RFC 1123: replace underscores with hyphens
        _jobName = jobName.Replace("_", "-");
        return this;
    }

    public SchemaValidationJobBuilder WithNamespace(string ns)
    {
        _namespace = ns;
        return this;
    }

    public SchemaValidationJobBuilder WithSchemaSubject(string schemaSubject)
    {
        _schemaSubject = schemaSubject;
        return this;
    }

    public SchemaValidationJobBuilder WithSchemaRegistryUrl(string url)
    {
        _schemaRegistryUrl = url;
        return this;
    }

    public SchemaValidationJobBuilder WithTimeout(int timeoutSeconds)
    {
        _timeoutSeconds = timeoutSeconds;
        return this;
    }

    public SchemaValidationJobBuilder WithRetryInterval(int retryIntervalSeconds)
    {
        _retryIntervalSeconds = retryIntervalSeconds;
        return this;
    }

    public SchemaValidationJobBuilder WithBackoffLimit(int backoffLimit)
    {
        _backoffLimit = backoffLimit;
        return this;
    }

    public SchemaValidationJobBuilder WithSchemaCompatibility(NamingConventionHelper.SchemaCompatibility compatibility)
    {
        _schemaCompatibility = compatibility;
        return this;
    }

    public SchemaValidationJobBuilder WithAutoSetCompatibility(bool enabled)
    {
        _autoSetCompatibility = enabled;
        return this;
    }

    public Job Build()
    {
        // Determine compatibility string for shell script
        var compatibilityValue = _schemaCompatibility.HasValue
            ? NamingConventionHelper.ToSchemaRegistryString(_schemaCompatibility.Value)
            : "";
        var autoSetValue = _autoSetCompatibility ? "true" : "false";

        // Shell script that polls Schema Registry until schema exists or timeout
        // Using $$""" so that {{var}} is C# interpolation, and {single} is literal shell brace
        var validationScript = $$"""
            #!/bin/sh
            set -e

            SCHEMA_REGISTRY_URL="{{_schemaRegistryUrl}}"
            SUBJECT="{{_schemaSubject}}"
            TIMEOUT={{_timeoutSeconds}}
            INTERVAL={{_retryIntervalSeconds}}
            SCHEMA_COMPATIBILITY="{{compatibilityValue}}"
            AUTO_SET_COMPATIBILITY="{{autoSetValue}}"

            echo "Checking schema subject: $SUBJECT"
            echo "Schema Registry: $SCHEMA_REGISTRY_URL"
            echo "Timeout: ${TIMEOUT}s, Retry interval: ${INTERVAL}s"
            if [ -n "$SCHEMA_COMPATIBILITY" ]; then
                echo "Schema compatibility: $SCHEMA_COMPATIBILITY (auto-set: $AUTO_SET_COMPATIBILITY)"
            fi

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

                    # Auto-set compatibility if enabled and specified
                    if [ "$AUTO_SET_COMPATIBILITY" = "true" ] && [ -n "$SCHEMA_COMPATIBILITY" ]; then
                        echo "Setting schema compatibility to: $SCHEMA_COMPATIBILITY"
                        compat_response=$(curl -s -o /dev/null -w "%{http_code}" \
                            -X PUT -H "Content-Type: application/json" \
                            -d "{\"compatibility\":\"$SCHEMA_COMPATIBILITY\"}" \
                            -u "$SCHEMA_REGISTRY_USERNAME:$SCHEMA_REGISTRY_PASSWORD" \
                            "$SCHEMA_REGISTRY_URL/config/$SUBJECT")

                        if [ "$compat_response" = "200" ]; then
                            echo "Compatibility set successfully to $SCHEMA_COMPATIBILITY"
                        else
                            echo "Warning: Failed to set compatibility (HTTP $compat_response)"
                            # Don't fail - schema exists, compatibility setting is optional
                        fi
                    fi

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
        var config = new
        {
            _jobName,
            _schemaSubject,
            _schemaRegistryUrl,
            _timeoutSeconds,
            _retryIntervalSeconds,
            _schemaCompatibility,
            _autoSetCompatibility
        };
        var json = JsonSerializer.Serialize(config);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
