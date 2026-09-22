namespace argocd.applications;

internal class StrimziOperator
{
    public StrimziOperator(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("strimzi-kafka-operator", provider, settings)
            .AddSource(ApplicationType.Helm)
            .Branch("0.47.0")
            .RepoUrl("quay.io/strimzi-helm")
            .AddValueFile($"$values/{settings.ManifestRoot}/strimzi-kafka-operator/values.yaml")
            .AddSource(ApplicationType.Yaml)
            .AsValueSource("values")
            .SyncWave(1)
            .Build();
    }
}
