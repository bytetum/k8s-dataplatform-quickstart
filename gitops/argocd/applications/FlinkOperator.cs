namespace argocd.applications;

internal class FlinkOperator
{
    public FlinkOperator(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("flink-kubernetes-operator", provider)
            .AddSource(ApplicationType.Helm)
            .SyncWave(1)
            .Branch("1.12.0")
            .RepoUrl("https://archive.apache.org/dist/flink/flink-kubernetes-operator-1.12.0/")
            .Build();
    }
}