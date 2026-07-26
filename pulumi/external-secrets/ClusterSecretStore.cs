using Kubernetes = Pulumi.Kubernetes;

namespace Pulumi.Crds.ExternalSecrets;

public class ClusterSecretStore : Kubernetes.ApiExtensions.CustomResource
{
    [Output("spec")]
    public Output<ClusterSecretStoreSpec> Spec { get; private set; } = null!;

    public ClusterSecretStore(string name, ClusterSecretStoreArgs args, CustomResourceOptions? options = null)
        : base(name, args, options)
    { }
}

[OutputType]
public sealed class ClusterSecretStoreSpec
{
}

public class ClusterSecretStoreArgs : Kubernetes.ApiExtensions.CustomResourceArgs
{
    [Input("spec")]
    public Input<ClusterSecretStoreSpecArgs>? Spec { get; set; }

    public ClusterSecretStoreArgs()
        : base("external-secrets.io/v1", "ClusterSecretStore")
    { }
}

public class ClusterSecretStoreSpecArgs : Pulumi.ResourceArgs
{
    [Input("provider")]
    public Input<ClusterSecretStoreSpecProviderArgs>? Provider { get; set; }

    [Input("conditions")]
    public InputList<ClusterSecretStoreSpecConditionsArgs>? Conditions { get; set; }
}

public class ClusterSecretStoreSpecConditionsArgs : Pulumi.ResourceArgs
{
    [Input("namespaces")]
    public InputList<string>? Namespaces { get; set; }
}

public class ClusterSecretStoreSpecProviderArgs : Pulumi.ResourceArgs
{
    [Input("kubernetes")]
    public Input<ClusterSecretStoreSpecProviderKubernetesArgs>? Kubernetes { get; set; }
}

public class ClusterSecretStoreSpecProviderKubernetesArgs : Pulumi.ResourceArgs
{
    [Input("remoteNamespace")]
    public Input<string>? RemoteNamespace { get; set; }

    [Input("server")]
    public Input<ClusterSecretStoreSpecProviderKubernetesServerArgs>? Server { get; set; }

    [Input("auth")]
    public Input<ClusterSecretStoreSpecProviderKubernetesAuthArgs>? Auth { get; set; }
}

public class ClusterSecretStoreSpecProviderKubernetesServerArgs : Pulumi.ResourceArgs
{
    [Input("caProvider")]
    public Input<ClusterSecretStoreSpecProviderKubernetesCaProviderArgs>? CaProvider { get; set; }
}

public class ClusterSecretStoreSpecProviderKubernetesCaProviderArgs : Pulumi.ResourceArgs
{
    [Input("type")]
    public Input<string>? Type { get; set; }

    [Input("name")]
    public Input<string>? Name { get; set; }

    [Input("key")]
    public Input<string>? Key { get; set; }

    [Input("namespace")]
    public Input<string>? Namespace { get; set; }
}

public class ClusterSecretStoreSpecProviderKubernetesAuthArgs : Pulumi.ResourceArgs
{
    [Input("serviceAccount")]
    public Input<ClusterSecretStoreSpecProviderKubernetesServiceAccountArgs>? ServiceAccount { get; set; }
}

public class ClusterSecretStoreSpecProviderKubernetesServiceAccountArgs : Pulumi.ResourceArgs
{
    [Input("name")]
    public Input<string>? Name { get; set; }

    [Input("namespace")]
    public Input<string>? Namespace { get; set; }
}
