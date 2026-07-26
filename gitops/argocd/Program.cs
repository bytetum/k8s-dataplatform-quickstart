global using Pulumi;
global using Kubernetes = Pulumi.Kubernetes;
using argocd.applications;

return await Deployment.RunAsync(() =>
{
    var config = new Config();
    _ = config.Require("kube_context");
    var settings = new ArgoApplicationSettings(
        config.Require("repo_url"),
        config.Require("target_revision"));

    _ = new ArgoApplications(
        config.Get("manifests_path") ?? "../manifests/argocd",
        config.Get("profile") ?? "full",
        settings);
    // TODO: add Projects, ApplicationSets if needed.
});
