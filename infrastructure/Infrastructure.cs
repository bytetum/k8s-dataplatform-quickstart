using infrastructure.gitops;
using Pulumi;

namespace infrastructure;

internal class Infrastructure : Stack
{
    public Infrastructure()
        : base()
    {
        var provider = new Pulumi.Kubernetes.Provider("cluster-provider", new()
        {
        });

        var argoCdInstallation = new ArgoCD(provider);
    }
}