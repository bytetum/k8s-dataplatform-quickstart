using System;
using System.Text.RegularExpressions;

namespace applications;

/// <summary>
/// Helper class for DD130 naming conventions across the data platform.
/// Provides consistent naming for topics, tables, jobs, and schema registry subjects.
/// </summary>
public static class NamingConventionHelper
{
    /// <summary>
    /// Data layer in the medallion architecture.
    /// </summary>
    public enum DataLayer
    {
        /// <summary>Raw ingested data, minimal transformation.</summary>
        Bronze,
        /// <summary>Cleaned, validated, and enriched data.</summary>
        Silver,
        /// <summary>Business-ready, aggregated data.</summary>
        Gold
    }

    /// <summary>
    /// Schema Registry compatibility modes.
    /// </summary>
    public enum SchemaCompatibility
    {
        /// <summary>No compatibility checking.</summary>
        None,
        /// <summary>Consumers using the new schema can read data produced with the previous schema.</summary>
        Backward,
        /// <summary>Consumers using the previous schema can read data produced with the new schema.</summary>
        Forward,
        /// <summary>Both backward and forward compatible.</summary>
        Full,
        /// <summary>Backward compatible with all previous versions.</summary>
        BackwardTransitive,
        /// <summary>Forward compatible with all previous versions.</summary>
        ForwardTransitive,
        /// <summary>Both backward and forward compatible with all previous versions.</summary>
        FullTransitive
    }

    /// <summary>
    /// Components that make up a DD130 topic name.
    /// </summary>
    /// <param name="Environment">Optional environment prefix (e.g., "dev", "prod").</param>
    /// <param name="Layer">Data layer (Bronze, Silver, Gold).</param>
    /// <param name="Domain">Business domain (e.g., "m3", "finance").</param>
    /// <param name="Subdomain">Optional subdomain for further categorization.</param>
    /// <param name="Dataset">Dataset name (e.g., "cidmas", "orders").</param>
    /// <param name="ProcessingStage">Optional processing stage (e.g., "raw", "cleaned", "aggregated").</param>
    public record TopicComponents(
        string? Environment,
        DataLayer Layer,
        string Domain,
        string? Subdomain,
        string Dataset,
        string? ProcessingStage);

    // Topic name pattern: [env.]<layer>.<domain>[.<subdomain>].<dataset>[.<stage>]
    private static readonly Regex TopicPattern = new(
        @"^(?:(?<env>[a-z0-9-]+)\.)?(?<layer>bronze|silver|gold)\.(?<domain>[a-z0-9-]+)(?:\.(?<subdomain>[a-z0-9-]+))?\.(?<dataset>[a-z0-9_-]+)(?:\.(?<stage>[a-z0-9-]+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a topic name into its DD130 components.
    /// </summary>
    /// <param name="topicName">The topic name to parse.</param>
    /// <returns>Parsed topic components.</returns>
    /// <exception cref="ArgumentException">Thrown when the topic name doesn't match DD130 format.</exception>
    public static TopicComponents ParseTopic(string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentException("Topic name cannot be null or empty.", nameof(topicName));

        var match = TopicPattern.Match(topicName);
        if (!match.Success)
            throw new ArgumentException($"Topic name '{topicName}' does not match DD130 naming convention.", nameof(topicName));

        var layerStr = match.Groups["layer"].Value.ToLowerInvariant();
        var layer = layerStr switch
        {
            "bronze" => DataLayer.Bronze,
            "silver" => DataLayer.Silver,
            "gold" => DataLayer.Gold,
            _ => throw new ArgumentException($"Unknown layer: {layerStr}")
        };

        return new TopicComponents(
            Environment: match.Groups["env"].Success ? match.Groups["env"].Value : null,
            Layer: layer,
            Domain: match.Groups["domain"].Value,
            Subdomain: match.Groups["subdomain"].Success && !string.IsNullOrEmpty(match.Groups["subdomain"].Value)
                ? match.Groups["subdomain"].Value
                : null,
            Dataset: match.Groups["dataset"].Value,
            ProcessingStage: match.Groups["stage"].Success && !string.IsNullOrEmpty(match.Groups["stage"].Value)
                ? match.Groups["stage"].Value
                : null);
    }

    /// <summary>
    /// Generates a topic name from DD130 components.
    /// </summary>
    /// <param name="components">The topic components.</param>
    /// <returns>A DD130-compliant topic name.</returns>
    public static string ToTopicName(TopicComponents components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var parts = new System.Collections.Generic.List<string>();

        if (!string.IsNullOrEmpty(components.Environment))
            parts.Add(components.Environment.ToLowerInvariant());

        parts.Add(components.Layer.ToString().ToLowerInvariant());
        parts.Add(components.Domain.ToLowerInvariant());

        if (!string.IsNullOrEmpty(components.Subdomain))
            parts.Add(components.Subdomain.ToLowerInvariant());

        parts.Add(components.Dataset.ToLowerInvariant());

        if (!string.IsNullOrEmpty(components.ProcessingStage))
            parts.Add(components.ProcessingStage.ToLowerInvariant());

        return string.Join(".", parts);
    }

    /// <summary>
    /// Generates an Iceberg table name from DD130 components.
    /// Format: ao_catalog.<layer>_<domain>.<dataset>[_<stage>]
    /// </summary>
    /// <param name="components">The topic components.</param>
    /// <returns>An Iceberg table name.</returns>
    public static string ToIcebergTable(TopicComponents components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var layer = components.Layer.ToString().ToLowerInvariant();
        var domain = components.Domain.ToLowerInvariant().Replace("-", "_");
        var dataset = components.Dataset.ToLowerInvariant().Replace("-", "_");

        var tableName = dataset;
        if (!string.IsNullOrEmpty(components.ProcessingStage))
        {
            var stage = components.ProcessingStage.ToLowerInvariant().Replace("-", "_");
            tableName = $"{dataset}_{stage}";
        }

        return $"ao_catalog.{layer}_{domain}.{tableName}";
    }

    /// <summary>
    /// Generates a Flink job name from DD130 components.
    /// Format: flink-<layer>-<domain>-<dataset>[-<subdomain>][-<stage>][-v<version>]
    /// </summary>
    public static string ToFlinkJobName(
        DataLayer targetLayer,
        string domain,
        string dataset,
        string? subdomain = null,
        string? processingStage = null,
        int? version = null,
        string? environment = null)
    {
        var parts = new System.Collections.Generic.List<string> { "flink" };

        if (!string.IsNullOrEmpty(environment))
            parts.Add(SanitizeForKubernetes(environment));

        parts.Add(targetLayer.ToString().ToLowerInvariant());
        parts.Add(SanitizeForKubernetes(domain));
        parts.Add(SanitizeForKubernetes(dataset));

        if (!string.IsNullOrEmpty(subdomain))
            parts.Add(SanitizeForKubernetes(subdomain));

        if (!string.IsNullOrEmpty(processingStage))
            parts.Add(SanitizeForKubernetes(processingStage));

        if (version.HasValue)
            parts.Add($"v{version.Value}");

        return string.Join("-", parts);
    }

    /// <summary>
    /// Generates a Dead Letter Queue (DLQ) topic name from a source topic.
    /// Format: <original_topic>.dlq
    /// </summary>
    /// <param name="sourceTopic">The source topic name.</param>
    /// <returns>A DLQ topic name.</returns>
    public static string ToDlqTopic(string sourceTopic)
    {
        if (string.IsNullOrWhiteSpace(sourceTopic))
            throw new ArgumentException("Source topic cannot be null or empty.", nameof(sourceTopic));

        return $"{sourceTopic}.dlq";
    }

    /// <summary>
    /// Gets the default schema compatibility for a data layer.
    /// Bronze = Backward (allow schema evolution with backward compat)
    /// Silver = Full (stricter, both directions)
    /// Gold = FullTransitive (strictest, business-critical)
    /// </summary>
    /// <param name="layer">The data layer.</param>
    /// <returns>The default schema compatibility.</returns>
    public static SchemaCompatibility GetDefaultCompatibility(DataLayer layer)
    {
        return layer switch
        {
            DataLayer.Bronze => SchemaCompatibility.Backward,
            DataLayer.Silver => SchemaCompatibility.Full,
            DataLayer.Gold => SchemaCompatibility.FullTransitive,
            _ => SchemaCompatibility.Backward
        };
    }

    /// <summary>
    /// Converts a SchemaCompatibility enum value to its Schema Registry string representation.
    /// </summary>
    /// <param name="compatibility">The schema compatibility.</param>
    /// <returns>The Schema Registry string value.</returns>
    public static string ToSchemaRegistryString(SchemaCompatibility compatibility)
    {
        return compatibility switch
        {
            SchemaCompatibility.None => "NONE",
            SchemaCompatibility.Backward => "BACKWARD",
            SchemaCompatibility.Forward => "FORWARD",
            SchemaCompatibility.Full => "FULL",
            SchemaCompatibility.BackwardTransitive => "BACKWARD_TRANSITIVE",
            SchemaCompatibility.ForwardTransitive => "FORWARD_TRANSITIVE",
            SchemaCompatibility.FullTransitive => "FULL_TRANSITIVE",
            _ => throw new ArgumentOutOfRangeException(nameof(compatibility), compatibility, "Unknown schema compatibility")
        };
    }

    /// <summary>
    /// Generates a connector name from DD130 components.
    /// Format: <layer>-<domain>-<dataset>[-<stage>]
    /// </summary>
    public static string ToConnectorName(
        DataLayer layer,
        string domain,
        string dataset,
        string? processingStage = null)
    {
        var parts = new System.Collections.Generic.List<string>
        {
            layer.ToString().ToLowerInvariant(),
            SanitizeForKubernetes(domain),
            SanitizeForKubernetes(dataset)
        };

        if (!string.IsNullOrEmpty(processingStage))
            parts.Add(SanitizeForKubernetes(processingStage));

        return string.Join("-", parts);
    }

    /// <summary>
    /// Sanitizes a string for use in Kubernetes resource names (RFC 1123).
    /// Replaces underscores with hyphens and converts to lowercase.
    /// </summary>
    private static string SanitizeForKubernetes(string value)
    {
        return value.ToLowerInvariant().Replace("_", "-");
    }
}
