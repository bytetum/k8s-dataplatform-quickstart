global using Pulumi;
global using Kubernetes = Pulumi.Kubernetes;
using argocd.applications;

return await Deployment.RunAsync(() =>
{
    var argoApplication = new ArgoApplications("../manifests/argocd");
    // TODO: add Projects, ApplicationSets if needed.
});
