namespace argocd.applications.flink_deployment;

internal class FlinkDeployment
{
    public FlinkDeployment(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("silver-m3-debug-customer-order-analytics", provider)
            .SyncWave(2)  // Flink must deploy before Kafka Connect to produce schemas
            .Build();
    }
}