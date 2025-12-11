namespace argocd.applications.flink_deployment;

internal class FlinkDeployment
{
    public FlinkDeployment(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("sql-runner-example", provider)
            .SyncWave(3)
            .Build();
    }
}