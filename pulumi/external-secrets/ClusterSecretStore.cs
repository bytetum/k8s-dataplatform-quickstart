﻿using Kubernetes = Pulumi.Kubernetes;

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
        : base("external-secrets.io/v1beta1", "ClusterSecretStore")
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
    [Input("scaleway")]
    public Input<ClusterSecretStoreSpecProviderScalewayArgs>? Scaleway { get; set; }
    
    [Input("fake")]
    public Input<ClusterSecretStoreSpecProviderFakeArgs>? Fake { get; set; }
}

//MARK: change
public class ClusterSecretStoreSpecProviderFakeArgs : Pulumi.ResourceArgs
{
    [Input("data")]
    public InputList<ClusterSecretStoreProviderDataFakeArgs>? Data { get; set; }
}

public class ClusterSecretStoreProviderDataFakeArgs : Pulumi.ResourceArgs
{
    [Input("key")]
    public Input<string>? Key { get; set; }
    [Input("value")]
    public Input<string>? Value { get; set; }
    
    [Input("valueMap")]
    public InputMap<string>? ValueMap { get; set; }

    [Input("version")]
    public Input<string>? Version { get; set; }
}

//MARK: endchange

public class ClusterSecretStoreSpecProviderScalewayArgs : Pulumi.ResourceArgs
{
    [Input("region")]
    public Input<string>? Region { get; set; }

    [Input("projectId")]
    public Input<string>? ProjectId { get; set; }

    [Input("accessKey")]
    public Input<ClusterSecretStoreSpecProviderScalewayAceessKeyArgs>? AccessKey { get; set; }

    [Input("secretKey")]
    public Input<ClusterSecretStoreSpecProviderScalewaySecretKeyArgs>? SecretKey { get; set; }
}

public class ClusterSecretStoreSpecProviderFakeSecretArgs : Pulumi.ResourceArgs
{
    [Input("key")]
    public InputMap<string>? value { get; set; }
}

public class ClusterSecretStoreSpecProviderScalewayAceessKeyArgs : Pulumi.ResourceArgs
{
    [Input("secretRef")]
    public Input<SecretRefArgs>? SecretRef { get; set; }
}

public class ClusterSecretStoreSpecProviderScalewaySecretKeyArgs : Pulumi.ResourceArgs
{
    [Input("secretRef")]
    public Input<SecretRefArgs>? SecretRef { get; set; }
}

public class SecretRefArgs : Pulumi.ResourceArgs
{
    [Input("name")]
    public Input<string>? Name { get; set; }

    [Input("key")]
    public Input<string>? Key { get; set; }
    
    [Input("namespace")]
    public Input<string>? Namespace { get; set; }
}
