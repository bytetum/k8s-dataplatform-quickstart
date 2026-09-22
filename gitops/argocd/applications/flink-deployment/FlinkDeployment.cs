namespace argocd.applications.flink_deployment;

internal class FlinkDeployment
{
    public FlinkDeployment(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        var application = new ArgoApplicationBuilder("silver-m3-debug-customer-order-analytics", provider, settings)
            .SyncWave(2);  // Flink must deploy before Kafka Connect to produce schemas
        if (settings.IsolatedNamespaces)
        {
            application = application
                .InNamespace("lakehouse-flink")
                .CreateNamespace();
        }
        application.Build();
    }
}
