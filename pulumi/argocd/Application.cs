namespace Pulumi.Crds.Argocd;

public class Application : Kubernetes.ApiExtensions.CustomResource
{
    [Output("spec")]
    public Output<ApplicationSpec> Spec { get; private set; } = null!;

    public Application(string name, ApplicationArgs args, CustomResourceOptions options)
        : base(name, args, options)
    { }
}

[OutputType]
public sealed class ApplicationSpec { }

public class ApplicationArgs : Kubernetes.ApiExtensions.CustomResourceArgs
{
    [Input("spec")]
    public required Input<ApplicationSpecArgs> Spec { get; set; }

    public ApplicationArgs()
        : base("argoproj.io/v1alpha1", "Application")
    { }
}

public class ApplicationSpecArgs : Pulumi.ResourceArgs
{
    [Input("project")]
    public Input<string> Project { get; set; } = "default";

    [Input("source")]
    public Input<ApplicationSpecSourceArgs>? Source { get; set; }

    [Input("sources")]
    public InputList<ApplicationSpecSourceArgs>? Sources { get; set; }

    [Input("destination")]
    public required Input<ApplicationSpecDestinationArgs> Destination { get; set; }

    [Input("syncPolicy")]
    public Input<ApplicationSpecSyncPolicyArgs>? SyncPolicy { get; set; }

    [Input("ignoreDifferences")]
    public InputList<ApplicationSpecIgnoreDifferencesArgs>? IgnoreDifferences { get; set; }
}

public class ApplicationSpecSourceArgs : Pulumi.ResourceArgs
{
    [Input("repoURL")]
    public required Input<string> RepoURL { get; set; }

    [Input("targetRevision")]
    public Input<string> TargetRevision { get; set; } = "HEAD";
    
    [Input("path")]
    public Input<string>? Path { get; set; }

    [Input("chart")]
    public Input<string>? Chart { get; set; }

    [Input("helm")]
    public Input<ApplicationSpecSourceHelmArgs>? Helm { get; set; }

    [Input("directory")]
    public Input<ApplicationSpecSourceDirectoryArgs>? Directory { get; set; }

    [Input("ref")]
    public Input<string>? Ref { get; set; }
}

public class ApplicationSpecSourceHelmArgs : Pulumi.ResourceArgs
{
    [Input("skipCrds")]
    public Input<bool>? SkipCrds { get; set; }

    [Input("valueFiles")]
    public InputList<string>? ValueFiles { get; set; }
}

public class ApplicationSpecSourceDirectoryArgs : Pulumi.ResourceArgs
{
    [Input("recurse")]
    public Input<bool>? Recurse { get; set; }

    [Input("exclude")]
    public Input<string>? Exclude {  get; set; }

    [Input("include")]
    public Input<string>? Include {  get; set; }
}

public class ApplicationSpecDestinationArgs : Pulumi.ResourceArgs
{
    [Input("namespace")]
    public required Input<string> Namespace { get; set; }

    [Input("server")]
    public Input<string> Server { get; set; } = "https://kubernetes.default.svc";
}

public class ApplicationSpecSyncPolicyArgs : Pulumi.ResourceArgs
{
    [Input("automated")]
    public Input<ApplicationSpecSyncPolicyAutomatedArgs>? Automated { get; set; }

    [Input("syncOptions")]
    public InputList<string>? SyncOptions { get; set; }
}

public class ApplicationSpecSyncPolicyAutomatedArgs : Pulumi.ResourceArgs
{
    [Input("prune")]
    public Input<bool>? Prune { get; set; }

    [Input("selfHeal")]
    public Input<bool>? SelfHeal { get; set; }
}

public class ApplicationSpecIgnoreDifferencesArgs : Pulumi.ResourceArgs
{
    [Input("group")]
    public Input<string>? Group { get; set; }

    [Input("kind")]
    public Input<string>? Kind { get; set; }

    [Input("jsonPointers")]
    public InputList<string>? JsonPointers { get; set; }

    [Input("jqPathExpressions")]
    public InputList<string>? JqPathExpressions { get; set; }

    [Input("managedFieldsManagers")]
    public InputList<string>? ManagedFieldsManagers { get; set; }

    [Input("name")]
    public Input<string>? Name { get; set; }

    [Input("namespace")]
    public Input<string>? Namespace { get; set; }
}
