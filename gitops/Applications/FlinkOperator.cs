namespace argocd.applications;

internal class FlinkOperator
{
    public FlinkOperator(Pulumi.Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("flink-operator", provider)
            .Type(ApplicationType.Helm)
            .RepoUrl("https://downloads.apache.org/flink/flink-kubernetes-operator-1.12.1/")
            .Branch("1.12.1")
            .Build();
    }
}