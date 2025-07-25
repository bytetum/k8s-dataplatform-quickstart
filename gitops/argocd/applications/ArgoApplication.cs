using Pulumi.Crds.Argocd;
using System.Collections.Generic;
using System.Linq;

namespace argocd.applications;

internal class ArgoApplicationBuilder(string name, Kubernetes.Provider provider)
{
    private string project = "default";
    private string destinationNamespace = name;
    private int syncWave = 0;
    private readonly List<ArgoApplicationSource> sources = [];

    public ArgoApplicationBuilder SyncWave(int syncWave)
    {
        this.syncWave = syncWave;
        return this;
    }
    
    private InputMap<string> Annotation => new()
    {
        { "argocd.argoproj.io/sync-wave", syncWave.ToString() },
    };

    public ArgoApplicationBuilder Project(string project)
    {
        this.project = project;
        return this;
    }

    public ArgoApplicationBuilder InNamespace(string destinationNamespace)
    {
        this.destinationNamespace = destinationNamespace;
        return this;
    }

    public ArgoApplicationBuilder Branch(string branch)
    {
        sources.Last().TargetRevision = branch;
        return this;
    }

    public ArgoApplicationBuilder RepoUrl(string repoURL)
    {
        sources.Last().RepoURL = repoURL;
        return this;
    }

    public ArgoApplicationBuilder AddSource(ApplicationType applicationType)
    {
        sources.Add(new(applicationType, name));
        return this;
    }

    public ArgoApplicationBuilder AddValueFile(string valueFile)
    {
        sources.Last().ValueFiles.Add(valueFile);
        return this;
    }

    public ArgoApplicationBuilder AsValueSource(string refName)
    {
        sources.Last().Ref = refName;
        return this;
    }

    public Kubernetes.ApiExtensions.CustomResource Build()
    {
        if (sources.Count == 0)
        {
            AddSource(ApplicationType.Yaml);
        }

        var syncPolicy = new ApplicationSpecSyncPolicyArgs
        {
            Automated = new ApplicationSpecSyncPolicyAutomatedArgs
            {
                Prune = true,
                SelfHeal = true,
            },
        };

        if (sources.Any(source => source.applicationType == ApplicationType.Helm))
        {
            syncPolicy.SyncOptions = ["CreateNamespace=true"];
        }

        var spec = new ApplicationSpecArgs
        {
            Project = project,
            Destination = new ApplicationSpecDestinationArgs
            {
                Namespace = destinationNamespace,
            },
            SyncPolicy = syncPolicy,
        };

        if (sources.Count > 1)
        {
            spec.Sources = sources.Select(source => (ApplicationSpecSourceArgs)source).ToList();
        }
        else
        {
            spec.Source = (ApplicationSpecSourceArgs)sources.First();
        }

        return new Application(name, new()
        {
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Annotations = Annotation,
                Name = name,
                Namespace = "argocd",
            },
            Spec = spec,
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

internal class ArgoApplicationSource(ApplicationType applicationType, string name)
{
    public readonly ApplicationType applicationType = applicationType;
    public string RepoURL { get; set; } = "git@github.com:bytetum/k8s-dataplatform-quickstart.git";
    public string TargetRevision { get; set; } = "HEAD";
    public string Path { get; set; } = $"gitops/manifests/{name}";
    public bool SkipCrds { get; set; } = false;
    public string Chart { get; set; } = name;
    public List<string> ValueFiles { get; set; } = [];
    public string? Ref { get; set; }

    public static explicit operator ApplicationSpecSourceArgs(ArgoApplicationSource source) => source.applicationType switch
    {
        ApplicationType.Yaml => new()
        {
            Path = source.Path,
            RepoURL = source.RepoURL,
            TargetRevision = source.TargetRevision,
            Directory = new ApplicationSpecSourceDirectoryArgs
            {
                Recurse = true,
            },
            Ref = source.Ref!,
        },
        ApplicationType.Helm => new()
        {
            Chart = source.Chart,
            RepoURL = source.RepoURL,
            TargetRevision = source.TargetRevision,
            Helm = source.SkipCrds || source.ValueFiles.Count > 0 ? new ApplicationSpecSourceHelmArgs
            {
                SkipCrds = source.SkipCrds ? true : null,
                ValueFiles = source.ValueFiles.Count > 0 ? source.ValueFiles : null!,
            } : null!,
        }
    };
}
