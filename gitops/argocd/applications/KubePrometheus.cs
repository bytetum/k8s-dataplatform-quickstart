namespace argocd.applications;

internal class KubePrometheus
{
    public KubePrometheus(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("kube-prometheus-stack", provider, settings)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://prometheus-community.github.io/helm-charts")
            .Branch("75.15.1")
            .ServerSide()
            .SyncWave(1)
            .Build();
    }
}
