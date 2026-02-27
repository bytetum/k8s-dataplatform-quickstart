namespace argocd.applications;

public class FlinkSessionMode
{
    public FlinkSessionMode(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("flink-session-mode", provider)
            .SyncWave(2)
            .InNamespace("flink-kubernetes-operator")
            .Build();
    }
}