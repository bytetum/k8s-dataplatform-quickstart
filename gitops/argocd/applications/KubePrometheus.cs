namespace argocd.applications;

internal class KubePrometheus
{
    public KubePrometheus(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("kube-prometheus-stack", provider)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("https://prometheus-community.github.io/helm-charts")
            .Branch("75.15.1")
            .ServerSide()
            .SyncWave(1)
            .Build();
    }
}
