namespace argocd.applications;

public class Marquez
{
    public Marquez(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("marquez", provider)
            .AddSource(ApplicationType.HelmGit)
            .RepoUrl("https://github.com/MarquezProject/marquez.git")
            .Branch("main")
            .Path("chart")
            .AddValueFile("$values/gitops/manifests/marquez/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(1)
            .InNamespace("marquez")
            .CreateNamespace()
            .Build();
    }
}
