using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Rbac.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Kubernetes.Types.Inputs.Rbac.V1;

namespace applications.infrastructure;

internal class Secrets : ComponentResource
{
    public Secrets(string manifestsRoot)
        : base("secrets", "secrets")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/secrets"
        }, new CustomResourceOptions
        {
            Parent = this
        });

        var resourceOptions = new CustomResourceOptions
        {
            Provider = provider,
            Parent = this,
        };

        var sourceNamespace = new Namespace("local-secrets-namespace", new NamespaceArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = SecretSources.Namespace,
            },
        }, resourceOptions);

        var readerServiceAccount = new ServiceAccount("local-secret-store-reader", new ServiceAccountArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = SecretSources.ReaderServiceAccountName,
                Namespace = SecretSources.Namespace,
            },
        }, resourceOptions);

        var readerRole = new Role("local-secret-store-reader", new RoleArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = SecretSources.ReaderServiceAccountName,
                Namespace = SecretSources.Namespace,
            },
            Rules = new InputList<PolicyRuleArgs>
            {
                new PolicyRuleArgs
                {
                    ApiGroups = new InputList<string> { "" },
                    Resources = new InputList<string> { "secrets" },
                    Verbs = new InputList<string> { "get", "list", "watch" },
                },
                new PolicyRuleArgs
                {
                    ApiGroups = new InputList<string> { "authorization.k8s.io" },
                    Resources = new InputList<string> { "selfsubjectrulesreviews" },
                    Verbs = new InputList<string> { "create" },
                },
            },
        }, resourceOptions);

        var readerRoleBinding = new RoleBinding("local-secret-store-reader", new RoleBindingArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = SecretSources.ReaderServiceAccountName,
                Namespace = SecretSources.Namespace,
            },
            RoleRef = new RoleRefArgs
            {
                ApiGroup = "rbac.authorization.k8s.io",
                Kind = "Role",
                Name = readerRole.Metadata.Apply(metadata => metadata.Name),
            },
            Subjects = new InputList<SubjectArgs>
            {
                new SubjectArgs
                {
                    Kind = "ServiceAccount",
                    Name = readerServiceAccount.Metadata.Apply(metadata => metadata.Name),
                    Namespace = SecretSources.Namespace,
                },
            },
        }, resourceOptions);

        var secretStore = new ClusterSecretStore("local-kubernetes-secret-store", new()
        {
            Metadata = new ObjectMetaArgs
            {
                Name = SecretSources.StoreName,
            },
            Spec = new ClusterSecretStoreSpecArgs
            {
                Conditions = new InputList<ClusterSecretStoreSpecConditionsArgs>
                {
                    new ClusterSecretStoreSpecConditionsArgs
                    {
                        Namespaces = new InputList<string>
                        {
                            Constants.KafkaConnectNamespace,
                            Constants.PolarisNamespace,
                            Constants.WarpStreamNamespace,
                            applications.flink.Constants.Namespace,
                        },
                    },
                },
                Provider = new ClusterSecretStoreSpecProviderArgs
                {
                    Kubernetes = new ClusterSecretStoreSpecProviderKubernetesArgs
                    {
                        RemoteNamespace = SecretSources.Namespace,
                        Server = new ClusterSecretStoreSpecProviderKubernetesServerArgs
                        {
                            CaProvider = new ClusterSecretStoreSpecProviderKubernetesCaProviderArgs
                            {
                                Type = "ConfigMap",
                                Name = "kube-root-ca.crt",
                                Key = "ca.crt",
                                Namespace = SecretSources.Namespace,
                            },
                        },
                        Auth = new ClusterSecretStoreSpecProviderKubernetesAuthArgs
                        {
                            ServiceAccount = new ClusterSecretStoreSpecProviderKubernetesServiceAccountArgs
                            {
                                Name = SecretSources.ReaderServiceAccountName,
                                Namespace = SecretSources.Namespace,
                            },
                        },
                    },
                },
            },
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = this,
            DependsOn = readerRoleBinding,
        });
    }
}
