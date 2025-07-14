using argocd.applications;
using infrastructure;
using Pulumi;

return await Deployment.RunAsync(() =>
{
    new Infrastructure();
    new ArgoApplications("../manifests");
});
