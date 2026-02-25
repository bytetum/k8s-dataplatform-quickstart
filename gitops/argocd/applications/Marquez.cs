namespace argocd.applications;

public class Marquez
{
    public Marquez(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("marquez", provider)
            .SyncWave(1) // Marquez must be available before Flink sends lineage events
            .InNamespace("marquez")
            .CreateNamespace()
            .Build();
    }
}
