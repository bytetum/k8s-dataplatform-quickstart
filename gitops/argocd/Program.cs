global using Pulumi;
global using Kubernetes = Pulumi.Kubernetes;
using argocd.applications;

return await Deployment.RunAsync(() =>
{
    var config = new Config();
    _ = config.Require("kube_context");
    var settings = new ArgoApplicationSettings(
        config.Require("repo_url"),
        config.Require("target_revision"),
        config.GetBoolean("auto_sync") ?? true,
        config.GetBoolean("auto_prune") ?? true,
        config.GetBoolean("self_heal") ?? true,
        config.Get("manifest_root") ?? "gitops/manifests",
        config.GetBoolean("reuse_existing_operators") ?? false,
        config.GetBoolean("isolated_namespaces") ?? false);

    _ = new ArgoApplications(
        config.Get("manifests_path") ?? "../manifests/argocd",
        config.Get("profile") ?? "full",
        settings);
    // TODO: add Projects, ApplicationSets if needed.
});
