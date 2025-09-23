namespace argocd.applications;

internal class StrimziOperator
{
    public StrimziOperator(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("strimzi-kafka-operator", provider)
            .AddSource(ApplicationType.Helm)
            .Branch("0.47.0")
            .RepoUrl("quay.io/strimzi-helm")
            .AddValueFile("$values/gitops/manifests/polaris/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(1)
            .Build();
    }
}