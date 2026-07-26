namespace argocd.applications;

internal class FlinkOperator
{
    public FlinkOperator(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("flink-kubernetes-operator", provider, settings)
            .AddSource(ApplicationType.Helm)
            .SyncWave(1)
            .Branch("1.13.0")
            .RepoUrl("https://archive.apache.org/dist/flink/flink-kubernetes-operator-1.13.0/")
            .Build();
    }
}
