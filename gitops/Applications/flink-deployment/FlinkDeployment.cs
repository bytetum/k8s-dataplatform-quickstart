namespace argocd.applications.flink_deployment;

internal class FlinkDeployment
{
    public FlinkDeployment(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("flink-deployment", provider)
            .SyncWave(3)
            .InNamespace("ns-flink")
            .Build();
    }
}