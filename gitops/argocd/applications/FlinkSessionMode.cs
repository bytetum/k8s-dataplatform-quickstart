namespace argocd.applications;

public class FlinkSessionMode
{
    public FlinkSessionMode(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("flink-session-mode", provider, settings)
            .SyncWave(2)
            .InNamespace("flink-kubernetes-operator")
            .Build();
    }
}
