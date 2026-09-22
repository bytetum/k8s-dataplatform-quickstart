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

        // The kind-local profile reuses the already-running cluster-wide
        // controllers (and their CRDs) instead of adopting or upgrading their
        // Deployments, webhooks, RBAC, or Helm releases.  Legacy stacks keep
        // their original ownership by leaving this switch disabled.
        if (!settings.ReuseExistingOperators)
        {
            _ = new CertManager(provider, settings);
            _ = new ExternalSecrets(provider, settings);
        }
        else
        {
            // Keep the local source namespace, RBAC, and ClusterSecretStore
            // in the kind-local graph even though the existing
            // external-secrets controller is reused.
            _ = new Secrets(provider, settings, "lakehouse-secrets");
        }

        if (Includes(selectedProfile, DeploymentProfile.Operators) && !settings.ReuseExistingOperators)
        {
            _ = new StrimziOperator(provider, settings);
            _ = new FlinkOperator(provider, settings);
        }

        if (Includes(selectedProfile, DeploymentProfile.Core))
        {
            _ = new WarpStream(provider, settings);
            _ = new WarpStreamSchemaRegistry(provider, settings);
            _ = new Polaris(provider, settings);
        }

        if (Includes(selectedProfile, DeploymentProfile.QueryLineage))
        {
            _ = new Trino(provider, settings);
            _ = new Marquez(provider, settings);
        }

        if (Includes(selectedProfile, DeploymentProfile.Processing))
        {
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
            "operators" => DeploymentProfile.Operators,
            "core" => DeploymentProfile.Core,
            "query" or "query-lineage" or "query_lineage" => DeploymentProfile.QueryLineage,
            "processing" => DeploymentProfile.Processing,
            "integration" => DeploymentProfile.Integration,
            "full" or "heavy-metadata" or "heavy_metadata" => DeploymentProfile.Full,
            _ => throw new System.ArgumentException(
                $"Unknown Argo application profile '{profile}'. " +
                "Expected foundation, operators, core, query-lineage, processing, integration, or full.",
                nameof(profile)),
        };

    private enum DeploymentProfile
    {
        Foundation,
        Operators,
        Core,
        QueryLineage,
        Processing,
        Integration,
        Full,
    }
}
