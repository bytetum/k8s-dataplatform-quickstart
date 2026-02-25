namespace argocd.applications;

public class OpenMetadataDependencies
{
    public OpenMetadataDependencies(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("openmetadata-dependencies", provider)
            .SyncWave(1)
            .InNamespace("openmetadata")
            .Build();
    }
}

public class OpenMetadata
{
    public OpenMetadata(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("openmetadata", provider)
            .SyncWave(2) // After dependencies are ready
            .InNamespace("openmetadata")
            .Build();
    }
}
