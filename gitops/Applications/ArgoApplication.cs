// File: ArgoApplicationBuilder.cs
using Pulumi;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using System.Text;

namespace argocd.applications;

// The builder class, now with Helm-specific capabilities.
internal class ArgoApplicationBuilder(string name, Pulumi.Kubernetes.Provider provider)
{
    private string project = "default";
    private string? destinationNamespace;
    private ApplicationType applicationType = ApplicationType.Yaml;
    private string targetRevision = "HEAD"; // Default for Git, should be overridden for Helm
    private string path = $"gitops/manifests/{name}";
    private string repoURL = "git@github.com:bytetum/k8s-dataplatform-quickstart.git";
    private int syncWave = 0;
    private string? helmValues;
    private bool addFinalizer = true;

    public ArgoApplicationBuilder SyncWave(int syncWave)
    {
        this.syncWave = syncWave;
        return this;
    }

    public ArgoApplicationBuilder Project(string project)
    {
        this.project = project;
        return this;
    }

    public ArgoApplicationBuilder Type(ApplicationType applicationType)
    {
        this.applicationType = applicationType;
        return this;
    }

    public ArgoApplicationBuilder InNamespace(string destinationNamespace)
    {
        this.destinationNamespace = destinationNamespace;
        return this;
    }

    // Use this for Git branches or Helm chart versions.
    public ArgoApplicationBuilder TargetRevision(string targetRevision)
    {
        this.targetRevision = targetRevision;
        return this;
    }
    
    // Use this to specify the Helm repository URL.
    public ArgoApplicationBuilder RepoUrl(string repoURL)
    {
        this.repoURL = repoURL;
        return this;
    }

    // New method to provide Helm values as a YAML string.
    public ArgoApplicationBuilder HelmValues(string values)
    {
        this.helmValues = values;
        return this;
    }

    public ArgoApplicationBuilder WithFinalizer(bool enable)
    {
        this.addFinalizer = enable;
        return this;
    }

    public void Build()
    {
        var annotations = new InputMap<string>
        {
            { "argocd.argoproj.io/sync-wave", syncWave.ToString() },
        };

        var finalizers = new InputList<string>();
        if (addFinalizer)
        {
            finalizers.Add("resources-finalizer.argoproj.io");
        }

        // --- SOURCE LOGIC ---
        // We build a single source object and populate it based on the type.
        var sourceArgs = new ArgoApplicationSourceArgs
        {
            RepoUrl = repoURL,
            TargetRevision = targetRevision,
        };

        if (applicationType == ApplicationType.Helm)
        {
            sourceArgs.Chart = name; // Assumes chart name is the same as the app name
            if (!string.IsNullOrEmpty(helmValues))
            {
                sourceArgs.Helm = new ArgoHelmArgs { Values = helmValues };
            }
        }
        else // Yaml type
        {
            sourceArgs.Path = path;
            sourceArgs.Directory = new InputMap<bool> { { "recurse", true } };
        }

        // --- SYNC POLICY LOGIC ---
        var syncPolicy = new ArgoApplicationSyncPolicyArgs
        {
            Automated = new InputMap<bool>
            {
                { "prune", true },
                { "selfHeal", true },
            },
            // Add CreateNamespace=true only for Helm apps, a common pattern.
            SyncOptions = applicationType == ApplicationType.Helm
                ? new InputList<string> { "CreateNamespace=true" }
                : new InputList<string>()
        };

        _ = new Pulumi.Kubernetes.ApiExtensions.CustomResource(name, new ArgoApplicationArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = name,
                Namespace = "argocd", // Applications are always in the argocd namespace
                Annotations = annotations,
                Finalizers = finalizers,
            },
            Spec = new ArgoApplicationSpecArgs
            {
                Project = project,
                Source = sourceArgs,
                Destination = new InputMap<string>
                {
                    { "server", "https://kubernetes.default.svc" },
                    { "namespace", destinationNamespace ?? name }
                },
                SyncPolicy = syncPolicy,
            }
        }, new()
        {
            Provider = provider,
        });
    }
}

enum ApplicationType
{
    Yaml,
    Helm
}

// --- ARGUMENT CLASSES ---
// These classes model the structure of the Argo CD Application CRD.

internal class ArgoApplicationArgs : Pulumi.Kubernetes.ApiExtensions.CustomResourceArgs
{
    [Input("spec")] public required Input<ArgoApplicationSpecArgs> Spec { get; set; }

    public ArgoApplicationArgs()
        : base("argoproj.io/v1alpha1", "Application")
    {
    }
}

internal class ArgoApplicationSpecArgs : ResourceArgs
{
    [Input("project")] public required Input<string> Project { get; set; }
    [Input("source")] public required Input<ArgoApplicationSourceArgs> Source { get; set; }
    [Input("destination")] public InputMap<string> Destination { get; set; } = [];
    [Input("syncPolicy")] public required Input<ArgoApplicationSyncPolicyArgs> SyncPolicy { get; set; }
}

// A single class for the 'source' block, containing optional fields for Git and Helm.
internal class ArgoApplicationSourceArgs : ResourceArgs
{
    [Input("repoURL")] public required Input<string> RepoUrl { get; set; }
    [Input("targetRevision")] public required Input<string> TargetRevision { get; set; }

    // Helm-specific fields
    [Input("chart")] public Input<string>? Chart { get; set; }
    [Input("helm")] public Input<ArgoHelmArgs>? Helm { get; set; }

    // Git-specific fields
    [Input("path")] public Input<string>? Path { get; set; }
    [Input("directory")] public InputMap<bool>? Directory { get; set; }
}

// New class to represent the 'helm' object within the source.
internal class ArgoHelmArgs : ResourceArgs
{
    [Input("values")]
    public Input<string>? Values { get; set; }
}

internal class ArgoApplicationSyncPolicyArgs : ResourceArgs
{
    [Input("automated")] public required InputMap<bool> Automated { get; set; }
    [Input("syncOptions")] public InputList<string> SyncOptions { get; set; } = [];
}