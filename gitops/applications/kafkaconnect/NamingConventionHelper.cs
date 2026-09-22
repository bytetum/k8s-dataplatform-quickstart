using System;
using System.Text.RegularExpressions;

namespace applications.kafkaconnect;

// DD130-compliant naming convention helper.
// Parses and generates names following the AO Essence Data Layer Structure.
//
// Kafka Topic: [environment.]layer.domain[.subdomain].dataset[_stage]
// Iceberg Table: layer.domain_dataset[_stage]
// Flink Job: [environment-]layer-domain[-subdomain]-dataset[-stage][-vN]
public static class NamingConventionHelper
{
    public enum DataLayer { Bronze, Silver, Gold }

    public enum SchemaCompatibility { None, Backward, Forward, Full }

    public record TopicComponents(
        string? Environment,
        DataLayer Layer,
        string Domain,
        string? Subdomain,
        string Dataset,
        string? ProcessingStage
    );

    public static TopicComponents ParseTopic(string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentException("Topic name cannot be empty", nameof(topicName));

        var parts = topicName.Split('.');

        // Minimum: layer.domain.dataset (3 parts)
        if (parts.Length < 3)
            throw new ArgumentException(
                $"Invalid topic format: '{topicName}'. Expected at least layer.domain.dataset",
                nameof(topicName));

        int index = 0;
        string? environment = null;
        DataLayer layer;

        // Check if first part is environment or layer
        if (TryParseLayer(parts[0], out layer))
        {
            // No environment prefix
            index = 1;
        }
        else if (parts.Length >= 4 && TryParseLayer(parts[1], out layer))
        {
            // Has environment prefix
            environment = parts[0];
            index = 2;
        }
        else
        {
            throw new ArgumentException(
                $"Cannot determine data layer from topic: '{topicName}'. " +
                $"Expected 'bronze', 'silver', or 'gold' as first or second segment.",
                nameof(topicName));
        }

        // Domain is always next
        var domain = parts[index++];

        // Remaining parts: could be subdomain(s) and dataset
        // Last part is always dataset (may include processing stage suffix)
        var remainingParts = parts.Length - index;

        string? subdomain = null;
        string datasetWithStage;

        if (remainingParts == 1)
        {
            // Just dataset
            datasetWithStage = parts[index];
        }
        else if (remainingParts == 2)
        {
            // Subdomain + dataset
            subdomain = parts[index];
            datasetWithStage = parts[index + 1];
        }
        else if (remainingParts > 2)
        {
            // Multiple subdomains (join with .)
            subdomain = string.Join(".", parts[index..(parts.Length - 1)]);
            datasetWithStage = parts[^1];
        }
        else
        {
            throw new ArgumentException(
                $"Invalid topic format: '{topicName}'. Missing dataset.",
                nameof(topicName));
        }

        // Parse processing stage from dataset (suffix after last underscore)
        var (dataset, processingStage) = ParseDatasetAndStage(datasetWithStage);

        return new TopicComponents(environment, layer, domain, subdomain, dataset, processingStage);
    }

    public static string ToTopicName(TopicComponents components)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (!string.IsNullOrEmpty(components.Environment))
            parts.Add(components.Environment);

        parts.Add(components.Layer.ToString().ToLowerInvariant());
        parts.Add(components.Domain);

        if (!string.IsNullOrEmpty(components.Subdomain))
            parts.Add(components.Subdomain);

        var dataset = components.Dataset;
        if (!string.IsNullOrEmpty(components.ProcessingStage))
            dataset = $"{dataset}_{components.ProcessingStage}";

        parts.Add(dataset);

        return string.Join(".", parts);
    }

    public static string ToIcebergTable(TopicComponents components)
    {
        var schema = components.Layer.ToString().ToLowerInvariant();

        var table = $"{components.Domain}_{components.Dataset}";
        if (!string.IsNullOrEmpty(components.ProcessingStage))
            table = $"{table}_{components.ProcessingStage}";

        return $"{schema}.{table}";
    }

    public static string ToIcebergTable(string topicName)
    {
        return ToIcebergTable(ParseTopic(topicName));
    }

    public static string ToDlqTopic(string sourceTopic) => $"{sourceTopic}_dlq";

    // Bronze=NONE, Silver=BACKWARD, Gold=FULL
    public static SchemaCompatibility GetDefaultCompatibility(DataLayer layer) => layer switch
    {
        DataLayer.Bronze => SchemaCompatibility.None,
        DataLayer.Silver => SchemaCompatibility.Backward,
        DataLayer.Gold => SchemaCompatibility.Full,
        _ => SchemaCompatibility.Backward
    };

    public static string ToSchemaRegistryString(SchemaCompatibility compatibility) => compatibility switch
    {
        SchemaCompatibility.None => "NONE",
        SchemaCompatibility.Backward => "BACKWARD",
        SchemaCompatibility.Forward => "FORWARD",
        SchemaCompatibility.Full => "FULL",
        _ => "BACKWARD"
    };

    public static string ToFlinkJobName(
        DataLayer targetLayer,
        string domain,
        string dataset,
        string? subdomain = null,
        string? processingStage = null,
        int? version = null,
        string? environment = null)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (!string.IsNullOrEmpty(environment))
            parts.Add(environment);

        parts.Add(targetLayer.ToString().ToLowerInvariant());
        parts.Add(domain);

        if (!string.IsNullOrEmpty(subdomain))
            parts.Add(subdomain);

        // Convert underscores to hyphens for Kubernetes compatibility
        var name = dataset.Replace("_", "-");
        if (!string.IsNullOrEmpty(processingStage))
            name = $"{name}-{processingStage.Replace("_", "-")}";
        if (version.HasValue)
            name = $"{name}-v{version}";

        parts.Add(name);

        // Use hyphens as separator (Flink operator doesn't allow dots in names)
        return string.Join("-", parts);
    }

    public static string ToConnectorName(TopicComponents components)
    {
        var parts = new System.Collections.Generic.List<string>
        {
            components.Layer.ToString().ToLowerInvariant(),
            components.Domain,
            components.Dataset.Replace("_", "-")
        };

        if (!string.IsNullOrEmpty(components.ProcessingStage))
            parts.Add(components.ProcessingStage.Replace("_", "-"));

        return string.Join("-", parts);
    }

    private static bool TryParseLayer(string value, out DataLayer layer)
    {
        return Enum.TryParse(value, ignoreCase: true, out layer);
    }

    private static (string dataset, string? processingStage) ParseDatasetAndStage(string datasetWithStage)
    {
        // Known processing stages from DD130
        string[] knownStages = { "raw", "cleaned", "enriched", "denormalized", "aggregated" };

        var lastUnderscore = datasetWithStage.LastIndexOf('_');
        if (lastUnderscore > 0)
        {
            var potentialStage = datasetWithStage[(lastUnderscore + 1)..];
            if (Array.Exists(knownStages, s => s.Equals(potentialStage, StringComparison.OrdinalIgnoreCase)))
            {
                return (datasetWithStage[..lastUnderscore], potentialStage);
            }
        }

        return (datasetWithStage, null);
    }
}
