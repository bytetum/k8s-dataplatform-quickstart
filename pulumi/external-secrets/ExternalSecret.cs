using Kubernetes = Pulumi.Kubernetes;

namespace Pulumi.Crds.ExternalSecrets;

public class ExternalSecret : Kubernetes.ApiExtensions.CustomResource
{
    [Output("spec")]
    public Output<ExternalSecretSpec> Spec { get; private set; } = null!;

    public ExternalSecret(string name, ExternalSecretArgs args, CustomResourceOptions options)
        : base(name, args, options)
    { }
}

[OutputType]
public sealed class ExternalSecretSpec { }

public class ExternalSecretArgs : Kubernetes.ApiExtensions.CustomResourceArgs
{
    [Input("spec")]
    public Input<ExternalSecretSpecArgs>? Spec { get; set; }

    public ExternalSecretArgs()
        : base("external-secrets.io/v1", "ExternalSecret")
    { }
}

public class ExternalSecretSpecArgs : Pulumi.ResourceArgs
{
    [Input("secretStoreRef")]
    public Input<ExternalSecretSpecSecretStoreRefArgs>? SecretStoreRef { get; set; }

    [Input("target")]
    public Input<ExternalSecretSpecTargetArgs>? Target { get; set; }

    [Input("dataFrom")]
    public InputList<ExternalSecretSpecDataFromArgs>? DataFrom { get; set; }
}

public class ExternalSecretSpecSecretStoreRefArgs : Pulumi.ResourceArgs
{
    [Input("kind")]
    public Input<string> Kind { get; set; } = "ClusterSecretStore";

    [Input("name")]
    public required Input<string> Name { get; set; }
}

public class ExternalSecretSpecTargetArgs : Pulumi.ResourceArgs
{
    [Input("name")]
    public required Input<string> Name { get; set; }

    [Input("creationPolicy")]
    public Input<string> CreationPolicy { get; set; } = "Owner";
}

public class ExternalSecretSpecDataFromArgs : Pulumi.ResourceArgs
{
    [Input("extract")]
    public required Input<ExternalSecretSpecDataFromExtractArgs> Extract { get; set; }
}

public class ExternalSecretSpecDataFromExtractArgs : Pulumi.ResourceArgs
{
    [Input("key")]
    public required Input<string> Key { get; set; }

    [Input("version")]
    public Input<string> Version { get; set; } = "latest_enabled";
}