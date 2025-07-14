using Pulumi;
using Kubernetes = Pulumi.Kubernetes;
using Pulumi.Kubernetes.Core.V1;

namespace infrastructure.gitops;

internal class ArgoCD : ComponentResource
{
    public ArgoCD(Kubernetes.Provider provider)
        : base("argocd-installation", "argocd-installation")
    {
        var config = new Config();

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

        var redisPasswordResource = new Pulumi.Random.RandomPassword("argo-redis-password", new()
        {
            Length = 16,
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
                { "auth", "conmemay" },
            },
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
        }, new()
        {
            Provider = provider,
            DependsOn = { redisSecret }
        });

        var repoCredentials = new Secret("repo-credentials", new()
        {
            Type = "Opaque",
            Data =
            {
                { "url", ToBase64("git@ssh-source.netcompany.com:22/tfs/Netcompany/AOJHECOM/_git/essence") },
                { "sshPrivateKey",  config.RequireSecret("argo_secret_key").Apply(ToBase64) },
                { "type", ToBase64("git") }
            },
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "repo-credentials",
                Namespace = ns.Metadata.Apply(metadata => metadata.Name),
                Labels =
                {
                    { "argocd.argoproj.io/secret-type", "repo-creds" }  
                }
            }
        }, new()
        {
            Provider = provider,
        });

        var repo = new Secret("repo", new()
        {
            Type = "Opaque",
            Data =
            {
                { "name", ToBase64("essence") },
                { "url", ToBase64("git@ssh-source.netcompany.com:22/tfs/Netcompany/AOJHECOM/_git/essence") },
                { "insecure", ToBase64("true") },
                { "type", ToBase64("git") },
            },
            Metadata = new Kubernetes.Types.Inputs.Meta.V1.ObjectMetaArgs
            {
                Name = "repo",
                Namespace = ns.Metadata.Apply(metadata => metadata.Name),
                Labels =
                {
                    { "argocd.argoproj.io/secret-type", "repository" }  
                }
            }
        }, new()
        {
            Provider = provider,
        });
        
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
                    Path = "gitops/manifests/argocd",
                    RepoUrl = "git@ssh-source.netcompany.com:22/tfs/Netcompany/AOJHECOM/_git/essence",
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
                SyncPolicy =
                {
                    {
                        "automated",
                        new InputMap<bool>
                        {
                            { "prune", true },
                            { "selfHeal", true },
                        }
                    }
                }
            }
        }, new()
        {
            Provider = provider,
            DependsOn = argoCd,
        });
    }

    private static string ToBase64(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return System.Convert.ToBase64String(bytes);
    }
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
    public Input<string> Branch = "HEAD";

    [Input("directory")]
    public InputMap<bool> Directory { get; set; } = [];
}
