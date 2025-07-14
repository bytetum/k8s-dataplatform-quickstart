namespace argocd.applications;

internal class FlinkOperator
{
    public FlinkOperator(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("flink-operator", provider)
            .Build();
    }
}