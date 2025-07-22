namespace argocd.applications.flink_deployment;

internal class FlinkDeployment
{
    public FlinkDeployment(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("flink-deployment", provider)
            .SyncWave(2)
            .Build();
    }
}