namespace argocd.applications.flink_deployment;

internal class FlinkDeployment
{
    public FlinkDeployment(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("sql-runner-example-script", provider)
            .SyncWave(2)  // Flink must deploy before Kafka Connect to produce schemas
            .Build();
    }
}