using Pulumi;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace argocd.applications;

internal class ArgoApplicationBuilder(string name, Pulumi.Kubernetes.Provider provider)
{
    private string project = "default";
    private string? destinationNamespace;
    private ApplicationType applicationType = ApplicationType.Yaml;
    private string branch = "HEAD";
    private string path = $"gitops/manifests/{name}";
    private string repoURL = "git@github.com:bytetum/k8s-dataplatform-quickstart.git";
    private int syncWave = 0;

    public ArgoApplicationBuilder SyncWave(int syncWave)
    {
        this.syncWave = syncWave;
        return this;
    }
    
    private InputMap<string> Annotation => new()
    {
        { "argocd.argoproj.io/sync-wave", syncWave.ToString() },
    };

    private InputMap<string> Destination => new()
    {
        { "server", "https://kubernetes.default.svc" },
        { "namespace", destinationNamespace ?? name }
    };

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

    public ArgoApplicationBuilder Branch(string branch)
    {
        this.branch = branch;
        return this;
    }

    public ArgoApplicationBuilder RepoUrl(string repoURL)
    {
        this.repoURL = repoURL;
        return this;
    }

    public void Build()
    {
        var source = applicationType switch
        {
            ApplicationType.Yaml => new ArgoApplicationSourceArgs
            {
                Path = path,
                RepoUrl = repoURL,
                Branch = branch,
                Directory =
                {
                    { "recurse", true }
                }
            },
            ApplicationType.Helm => new ArgoHelmApplicationSourceArgs
            {
                Chart = name,
                RepoUrl = repoURL,
                Branch = branch,
                SkipCrds = false,
            },
        };

        var syncPolicy = applicationType switch
        {
            ApplicationType.Yaml => new ArgoApplicationSyncPolicyArgs
            {
                Automated = new InputMap<bool>
                {
                    { "prune", true },
                    { "selfHeal", true },
                },
            },
            ApplicationType.Helm => new ArgoApplicationSyncPolicyArgs
            {
                Automated = new InputMap<bool>
                {
                    { "prune", true },
                    { "selfHeal", true },
                },
                SyncOptions =
                [
                    "CreateNamespace=true",
                ],
            },
        };

        var application = new Pulumi.Kubernetes.ApiExtensions.CustomResource(name, new ArgoApplicationArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Annotations = Annotation,
                Name = name,
            },
            Spec = new ArgoApplicationSpecArgs
            {
                Project = project,
                Source = source,
                Destination = Destination,
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

internal class ArgoApplicationSyncPolicyArgs : ResourceArgs
{
    [Input("automated")] public required InputMap<bool> Automated { get; set; }

    [Input("syncOptions")] public InputList<string> SyncOptions { get; set; } = [];
}

internal class ArgoApplicationSourceArgs : ResourceArgs
{
    [Input("path")] public Input<string> Path { get; set; } = "";

    [Input("repoURL")] public required Input<string> RepoUrl { get; set; }

    [Input("targetRevision")] public Input<string> Branch = "HEAD";

    [Input("directory")] public InputMap<bool> Directory { get; set; } = [];
}

internal class ArgoHelmApplicationSourceArgs : ArgoApplicationSourceArgs
{
    [Input("chart")] public required Input<string> Chart { get; set; }

    [Input("skipCrds")] public required Input<bool> SkipCrds { get; set; }
}