using System.Collections.Generic;
using System.IO;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.flink.flink_deployment;

internal class FlinkDeployment : ComponentResource
{
    public FlinkDeployment(string deploymentName, string manifestsRoot) : base(
        "flink-deployment",
        "flink-deployment")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/{deploymentName}"
        }, new CustomResourceOptions
        {
            Parent = this
        });

        var flinkImpersonateRole = new Pulumi.Kubernetes.Rbac.V1.ClusterRole("flink-impersonate-role", new()
        {
            Metadata = new ObjectMetaArgs { Name = "flink-impersonate" },
            Rules = new[]
            {
                new Pulumi.Kubernetes.Types.Inputs.Rbac.V1.PolicyRuleArgs
                {
                    ApiGroups = new[] { "" },
                    Resources = new[] { "serviceaccounts" },
                    Verbs = new[] { "impersonate" }
                }
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = this
        });

        var flinkImpersonateRoleBinding = new Pulumi.Kubernetes.Rbac.V1.ClusterRoleBinding("flink-impersonate-rb", new()
        {
            Metadata = new ObjectMetaArgs { Name = "flink-impersonate-binding" },
            Subjects = new[]
            {
                new Pulumi.Kubernetes.Types.Inputs.Rbac.V1.SubjectArgs
                {
                    Kind = "ServiceAccount",
                    Name = "flink-operator",
                    Namespace = "flink-kubernetes-operator"
                }
            },
            RoleRef = new Pulumi.Kubernetes.Types.Inputs.Rbac.V1.RoleRefArgs
            {
                Kind = "ClusterRole",
                Name = flinkImpersonateRole.Metadata.Apply(m => m.Name),
                ApiGroup = "rbac.authorization.k8s.io"
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = this
        });

        var flinkKafkaCredentialsSecret = new ExternalSecret("flink-warpstream-credentials-secret",
            new ExternalSecretArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "flink-warpstream-credentials-secret",
                    Namespace = Constants.Namespace
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
                        Name = "flink-warpstream-credentials-secret"
                    },
                    DataFrom = new ExternalSecretSpecDataFromArgs()
                    {
                        Extract = new ExternalSecretSpecDataFromExtractArgs()
                        {
                            Key = "id:flink-warpstream-credentials-secret"
                        }
                    }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });

        var sqlFileContent = File.ReadAllText("./flink/flink-deployment/test_job.sql");
        var flinkDeploymentSql = new Kubernetes.ApiExtensions.CustomResource("flink-deployment-sql",
            new FlinkDeploymentArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "basic-checkpoint-ha-sql-example",
                    Namespace = Constants.Namespace,
                },
                Spec = new Dictionary<string, object>
                {
                    ["image"] = "flink:1.20",
                    ["flinkVersion"] = "v1_20",
                    ["flinkConfiguration"] = new Dictionary<string, object>
                    {
                        ["taskmanager.numberOfTaskSlots"] = "2",
                        ["jobmanager.scheduler"] = "adaptive",
                        // Add additional debug/logging configuration
                        ["env.java.opts"] = "-verbose:gc -XX:+PrintGCDetails",
                        // Kafka configuration
                        ["kafka.bootstrap.servers"] = "warpstream-agent.default.svc.cluster.local:9092",
                        ["kafka.input.topic"] = "input-topic",
                        ["kafka.output.topic"] = "output-topic",
                    },
                    ["serviceAccount"] = "flink",
                    ["jobManager"] = new Dictionary<string, object>
                    {
                        ["resource"] = new Dictionary<string, object>
                        {
                            ["memory"] = "2048m",
                            ["cpu"] = 1,
                        },
                    },
                    ["podTemplate"] = new Dictionary<string, object>
                    {
                        ["spec"] = new Dictionary<string, object>
                        {
                            ["containers"] = new[]
                            {
                                new Dictionary<string, object>
                                {
                                    ["name"] = "flink-main-container",
                                    ["envFrom"] = new[]
                                    {
                                        new Dictionary<string, object>
                                        {
                                            ["secretRef"] = new Dictionary<string, object>
                                            {
                                                ["name"] = "flink-warpstream-credentials-secret"
                                            }
                                        }
                                    }
                                },
                            }
                        }
                    },
                    ["service"] = new Dictionary<string, object>
                    {
                        ["type"] = "ClusterIP",
                        ["port"] = 8081,
                        ["targetPort"] = 8081
                    },
                    ["taskManager"] = new Dictionary<string, object>
                    {
                        ["resource"] = new Dictionary<string, object>
                        {
                            ["memory"] = "2048m",
                            ["cpu"] = 1
                        }
                    },
                    // Add the job configuration
                    ["job"] = new Dictionary<string, object>
                    {
                        ["jarURI"] = "https://repo.maven.apache.org/maven2/org/apache/flink/flink-sql-client/1.17.1/flink-sql-client-1.17.1.jar",
                        ["entryClass"] = "org.apache.flink.table.client.SqlClient",
                        ["args"] = new[]
                        {
                            "-i",
                            "-e",
                            sqlFileContent
                        },
                        ["parallelism"] = 1,
                        ["upgradeMode"] = "stateless"
                    }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this,
                DependsOn = new[] { flinkImpersonateRoleBinding }
            });
    }

    private class FlinkDeploymentArgs : Kubernetes.ApiExtensions.CustomResourceArgs
    {
        public FlinkDeploymentArgs() : base("flink.apache.org/v1beta1", "FlinkDeployment")
        {
        }

        [Input("spec")] public Dictionary<string, object>? Spec { get; set; }
    }
}