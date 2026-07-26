using argocd.applications.flink_deployment;
using argocd.applications;
using argocd.applications.kafka_connect;

namespace argocd.applications;

internal class ArgoApplications : ComponentResource
{
    public ArgoApplications(
        string manifestsRoot,
        string profile,
        ArgoApplicationSettings settings)
        : base("manifests", "argo-applications")
    {
        var provider = new Kubernetes.Provider("argocd-application-provider", new()
        {
            RenderYamlToDirectory = manifestsRoot,
        });

        var selectedProfile = ParseProfile(profile);

        _ = new CertManager(provider, settings);
        _ = new ExternalSecrets(provider, settings);

        if (Includes(selectedProfile, DeploymentProfile.Core))
        {
            _ = new WarpStream(provider, settings);
            _ = new WarpStreamSchemaRegistry(provider, settings);
            _ = new StrimziOperator(provider, settings);
            _ = new Polaris(provider, settings);
        }

        if (Includes(selectedProfile, DeploymentProfile.QueryLineage))
        {
            _ = new Trino(provider, settings);
            _ = new Marquez(provider, settings);
        }

        if (Includes(selectedProfile, DeploymentProfile.Processing))
        {
            _ = new FlinkOperator(provider, settings);
            _ = new FlinkDeployment(provider, settings);
            _ = new FlinkSessionMode(provider, settings);
        }

        if (Includes(selectedProfile, DeploymentProfile.Integration))
        {
            _ = new KafkaConnect(provider, settings);
        }

        if (Includes(selectedProfile, DeploymentProfile.Full))
        {
            _ = new OpenMetadataDependencies(provider, settings);
            _ = new OpenMetadata(provider, settings);
        }

        // KubePrometheus remains disabled, matching the previous default.
    }

    private static bool Includes(DeploymentProfile selected, DeploymentProfile required) =>
        (int)selected >= (int)required;

    private static DeploymentProfile ParseProfile(string profile) =>
        profile.Trim().ToLowerInvariant() switch
        {
            "foundation" => DeploymentProfile.Foundation,
            "core" => DeploymentProfile.Core,
            "query" or "query-lineage" or "query_lineage" => DeploymentProfile.QueryLineage,
            "processing" => DeploymentProfile.Processing,
            "integration" => DeploymentProfile.Integration,
            "full" or "heavy-metadata" or "heavy_metadata" => DeploymentProfile.Full,
            _ => throw new System.ArgumentException(
                $"Unknown Argo application profile '{profile}'. " +
                "Expected foundation, core, query-lineage, processing, integration, or full.",
                nameof(profile)),
        };

    private enum DeploymentProfile
    {
        Foundation,
        Core,
        QueryLineage,
        Processing,
        Integration,
        Full,
    }
}
