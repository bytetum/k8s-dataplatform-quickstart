namespace argocd.applications;

public class Marquez
{
    public Marquez(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("marquez", provider, settings)
            .AddSource(ApplicationType.HelmGit)
            .RepoUrl("https://github.com/MarquezProject/marquez.git")
            .Branch("main")
            .Path("chart")
            .AddValueFile($"$values/{settings.ManifestRoot}/marquez/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(1)
            .InNamespace(settings.WorkloadNamespace("marquez", "lakehouse-marquez"))
            .CreateNamespace()
            .Build();
    }
}
