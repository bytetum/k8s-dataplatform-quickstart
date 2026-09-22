namespace argocd.applications;

public class OpenMetadataDependencies
{
    public OpenMetadataDependencies(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("openmetadata-dependencies", provider, settings)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://helm.open-metadata.org/")
            .Chart("openmetadata-dependencies")
            .Branch("1.12.1")
            .AddValueFile($"$values/{settings.ManifestRoot}/openmetadata-dependencies/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(1)
            .InNamespace(settings.WorkloadNamespace("openmetadata", "lakehouse-openmetadata"))
            .CreateNamespace()
            .ServerSide()
            .Build();
    }
}

public class OpenMetadata
{
    public OpenMetadata(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("openmetadata", provider, settings)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://helm.open-metadata.org/")
            .Chart("openmetadata")
            .Branch("1.12.1")
            .AddValueFile($"$values/{settings.ManifestRoot}/openmetadata/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(2) // After dependencies are ready
            .InNamespace(settings.WorkloadNamespace("openmetadata", "lakehouse-openmetadata"))
            .CreateNamespace()
            .Build();
    }
}
