namespace argocd.applications;

internal class StrimziOperator
{
    public StrimziOperator(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("strimzi-kafka-operator", provider)
            .AddSource(ApplicationType.Helm)
            .RepoUrl("quay.io/strimzi-helm")
            .Branch("0.47.0")
            .SyncWave(1)
            .Build();
    }
}