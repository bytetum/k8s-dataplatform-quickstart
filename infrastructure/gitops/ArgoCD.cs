using System.Collections.Generic;
using Pulumi;
using Kubernetes = Pulumi.Kubernetes;
using Pulumi.Kubernetes.Core.V1;

namespace infrastructure.gitops;

internal class ArgoCD : ComponentResource
{
    public ArgoCD(
        Kubernetes.Provider provider,
        string repoUrl,
        string targetRevision,
        string manifestsPath,
        bool automatedSync,
        bool automatedPrune,
        bool automatedSelfHeal)
        : base("argocd-installation", "argocd-installation")
    {
        var ns = new Namespace("ns-argocd", new()
        {
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "argocd"
            }
        }, new()
        {
          Provider = provider,
        });

        var redisPassword = new Pulumi.Random.RandomPassword("argo-redis-password", new()
        {
            Length = 32,
            Special = false,
        });

        var redisSecret = new Secret("argo-redis-secret", new()
        {
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "argocd-redis",
                Namespace = ns.Metadata.Apply(metadata => metadata.Name),
            },
            Type = "Opaque",
            StringData =
            {
                { "auth", redisPassword.Result },
            },
        }, new()
        {
            Provider = provider,
        });

        var argoCd = new Kubernetes.Helm.V4.Chart("argocd", new()
        {
            Namespace = ns.Metadata.Apply(metadata => metadata.Name),
            Chart = "argo-cd",
            Version = "8.1.2",
            RepositoryOpts = new Kubernetes.Types.Inputs.Helm.V4.RepositoryOptsArgs
            {
                Repo = "https://argoproj.github.io/argo-helm",
            },
            Values =
            {
                ["server"] = new Dictionary<string, object>
                {
                    ["readinessProbe"] = LaptopProbeSettings(),
                    ["livenessProbe"] = LaptopProbeSettings(),
                },
                ["repoServer"] = new Dictionary<string, object>
                {
                    ["readinessProbe"] = LaptopProbeSettings(),
                    ["livenessProbe"] = LaptopProbeSettings(),
                },
            },
        }, new()
        {
            Provider = provider,
            DependsOn = redisSecret,
        });
        
        var syncPolicy = new InputMap<InputMap<bool>>();
        if (automatedSync)
        {
            syncPolicy.Add("automated", new InputMap<bool>
            {
                { "prune", automatedPrune },
                { "selfHeal", automatedSelfHeal },
            });
        }

        var applications = new Kubernetes.ApiExtensions.CustomResource("applications", new ArgoApplicationArgs
        {
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "applications",
                Namespace = ns.Metadata.Apply(metadata => metadata.Name),
            },
            Spec = new ArgoApplicationSpecArgs
            {
                Source = new ArgoApplicationSourceArgs
                {
                    Path = manifestsPath,
                    RepoUrl = repoUrl,
                    TargetRevision = targetRevision,
                    Directory =
                    {
                        { "recurse", true }
                    }
                },
                Destination =
                {
                    { "server", "https://kubernetes.default.svc" },
                    { "namespace", "argocd" }
                },
                SyncPolicy = syncPolicy
            }
        }, new()
        {
            Provider = provider,
            DependsOn = argoCd,
        });
    }

    private static Dictionary<string, object> LaptopProbeSettings() => new()
    {
        ["failureThreshold"] = 6,
        ["initialDelaySeconds"] = 10,
        ["periodSeconds"] = 10,
        ["successThreshold"] = 1,
        ["timeoutSeconds"] = 5,
    };
}
internal class ArgoApplicationArgs : Kubernetes.ApiExtensions.CustomResourceArgs
{
    [Input("spec")]
    public required Input<ArgoApplicationSpecArgs> Spec { get; set; }

    public ArgoApplicationArgs()
        : base("argoproj.io/v1alpha1", "Application")
    { }

}
internal class ArgoApplicationSpecArgs : ResourceArgs
{
    [Input("project")]
    public Input<string> Project { get; set; } = "default";

    [Input("source")]
    public required Input<ArgoApplicationSourceArgs> Source { get; set; }

    [Input("destination")]
    public InputMap<string> Destination { get; set; } = [];

    [Input("syncPolicy")]
    public InputMap<InputMap<bool>> SyncPolicy { get; set; } = [];
}

internal class ArgoApplicationSourceArgs: ResourceArgs
{
    [Input("path")]
    public required Input<string> Path { get; set; }

    [Input("repoURL")]
    public required Input<string> RepoUrl { get; set; }

    [Input("targetRevision")]
    public required Input<string> TargetRevision { get; set; }

    [Input("directory")]
    public InputMap<bool> Directory { get; set; } = [];
}
