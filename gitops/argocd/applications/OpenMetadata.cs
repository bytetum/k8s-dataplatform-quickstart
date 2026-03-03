namespace argocd.applications;

public class OpenMetadataDependencies
{
    public OpenMetadataDependencies(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("openmetadata-dependencies", provider)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://helm.open-metadata.org/")
            .Chart("openmetadata-dependencies")
            .Branch("1.12.1")
            .AddValueFile("$values/gitops/manifests/openmetadata-dependencies/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(1)
            .InNamespace("openmetadata")
            .CreateNamespace()
            .ServerSide()
            .Build();
    }
}

public class OpenMetadata
{
    public OpenMetadata(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("openmetadata", provider)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://helm.open-metadata.org/")
            .Chart("openmetadata")
            .Branch("1.12.1")
            .AddValueFile("$values/gitops/manifests/openmetadata/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(2) // After dependencies are ready
            .InNamespace("openmetadata")
            .CreateNamespace()
            .Build();
    }
}
