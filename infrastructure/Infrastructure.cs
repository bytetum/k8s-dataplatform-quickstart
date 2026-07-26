using infrastructure.gitops;
using Pulumi;

namespace infrastructure;

internal class Infrastructure : Stack
{
    public Infrastructure()
        : base()
    {
        var config = new Config();
        var kubeContext = config.Require("kube_context");
        var repoUrl = config.Require("repo_url");
        var targetRevision = config.Require("target_revision");
        var manifestsPath = config.Get("manifests_path") ?? "gitops/manifests/argocd";

        var provider = new Pulumi.Kubernetes.Provider("cluster-provider", new()
        {
            Context = kubeContext,
        });

        _ = new ArgoCD(provider, repoUrl, targetRevision, manifestsPath);
    }
}
