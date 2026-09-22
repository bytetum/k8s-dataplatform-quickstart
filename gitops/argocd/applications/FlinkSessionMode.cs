namespace argocd.applications;

public class FlinkSessionMode
{
    public FlinkSessionMode(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        var application = new ArgoApplicationBuilder("flink-session-mode", provider, settings)
            .SyncWave(2)
            .InNamespace(settings.WorkloadNamespace("flink-kubernetes-operator", "lakehouse-flink"));
        if (settings.IsolatedNamespaces)
        {
            application = application.CreateNamespace();
        }
        application.Build();
    }
}
