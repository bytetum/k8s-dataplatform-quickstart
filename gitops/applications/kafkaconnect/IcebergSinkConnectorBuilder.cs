using System;
using System.Collections.Generic;
using Pulumi;
using Pulumi.Crds.KafkaConnect;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

/// <summary>
/// Builder for creating Iceberg Sink Connectors with sensible defaults.
/// Only requires table-specific configuration; all common settings are pre-configured.
/// </summary>
public class IcebergSinkConnectorBuilder
{
    private string _manifestsRoot = "";
    private string _connectorName = "";
    private string _sourceTopic = "";
    private string? _topicsRegex;
    private string _destinationTable = "";
    private string? _idColumns;
    private string? _defaultIdColumns;
    private string? _partitionBy;
    private string _clusterName = "m3-kafka-connect";
    private int _tasksMax = 1;
    private bool _upsertModeEnabled = true;

    // Dynamic Routing Configuration
    private bool _dynamicRoutingEnabled = false;
    private string? _routeField;

    // Schema Registry Cache Configuration
    private int? _cacheSize;
    private int? _cacheTtlMs;

    // Error Handling Configuration
    private bool _failFastMode = false;
    private int? _retryDelayMaxMs;
    private int? _retryTimeoutMs;

    // Custom Control Topic Configuration
    private string? _controlTopic;
    private string? _controlGroupIdPrefix;

    // Commit Interval Configuration
    private int _commitIntervalMs = 60000;

    // SMT Configuration
    private string? _regexRouterPattern;
    private string? _regexRouterReplacement;

    // Common defaults (can be overridden)
    private string _schemaRegistryUrl =
        "http://warpstream-schema-registry-warpstream-agent.warpstream.svc.cluster.local:9094";

    private string _schemaRegistryAuth = "${env:SCHEMA_REGISTRY_USERNAME}:${env:SCHEMA_REGISTRY_PASSWORD}";

    private string _polarisUri = "http://polaris.polaris.svc.cluster.local:8181/api/catalog";
    private string _catalogName = "ao_catalog";
    private string _dlqTopic = "m3.iceberg.dlq";

    /// <summary>
    /// Create a new Iceberg Sink Connector builder.
    /// </summary>
    /// <param name="manifestsRoot">Root directory for generated manifests</param>
    public IcebergSinkConnectorBuilder(string manifestsRoot)
    {
        _manifestsRoot = manifestsRoot;
    }

    /// <summary>
    /// Set the connector name (required).
    /// </summary>
    /// <param name="connectorName">Unique name for this connector (e.g., "cidmas")</param>
    public IcebergSinkConnectorBuilder WithConnectorName(string connectorName)
    {
        _connectorName = connectorName;
        return this;
    }

    /// <summary>
    /// Set the source Kafka topic (required).
    /// </summary>
    /// <param name="sourceTopic">Source Kafka topic (e.g., "bronze.m3.cidmas")</param>
    public IcebergSinkConnectorBuilder WithSourceTopic(string sourceTopic)
    {
        _sourceTopic = sourceTopic;
        _topicsRegex = null; // Clear regex if explicit topic is set
        return this;
    }

    /// <summary>
    /// Set the source Kafka topics using a regex pattern.
    /// Mutually exclusive with WithSourceTopic.
    /// </summary>
    /// <param name="topicsRegex">Regex pattern for topics (e.g., "silver\\.m3\\..*")</param>
    public IcebergSinkConnectorBuilder WithTopicsRegex(string topicsRegex)
    {
        _topicsRegex = topicsRegex;
        _sourceTopic = ""; // Clear explicit topic if regex is set
        return this;
    }

    /// <summary>
    /// Set the destination Iceberg table (required for single-topic connectors).
    /// For regex-based connectors, tables are auto-created based on topic names.
    /// </summary>
    /// <param name="destinationTable">Destination Iceberg table (e.g., "m3_bronze.cidmas")</param>
    public IcebergSinkConnectorBuilder WithDestinationTable(string destinationTable)
    {
        _destinationTable = destinationTable;
        return this;
    }

    /// <summary>
    /// Set the ID columns for upsert mode (comma-separated).
    /// Example: "idcono,idsuno" or "iisuno"
    /// </summary>
    public IcebergSinkConnectorBuilder WithIdColumns(params string[] idColumns)
    {
        _idColumns = string.Join(",", idColumns);
        return this;
    }

    /// <summary>
    /// Set the default ID columns for all tables (global upsert strategy).
    /// Example: "record_key" or "id"
    /// </summary>
    public IcebergSinkConnectorBuilder WithDefaultIdColumns(string defaultIdColumns)
    {
        _defaultIdColumns = defaultIdColumns;
        return this;
    }

    /// <summary>
    /// Set the partition column for the Iceberg table.
    /// Example: "idcono" or "iisugr"
    /// </summary>
    public IcebergSinkConnectorBuilder WithPartitionBy(string partitionColumn)
    {
        _partitionBy = partitionColumn;
        return this;
    }

    /// <summary>
    /// Enable or disable upsert mode (default: true).
    /// Set to false for append-only event streams.
    /// </summary>
    public IcebergSinkConnectorBuilder WithUpsertMode(bool enabled)
    {
        _upsertModeEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Configure RegexRouter SMT to transform topic names to table names.
    /// Example: pattern="silver\\.m3\\.(.*)", replacement="$1"
    /// Transforms "silver.m3.customer_summary" → "customer_summary"
    /// </summary>
    public IcebergSinkConnectorBuilder WithRegexRouter(string pattern, string replacement)
    {
        _regexRouterPattern = pattern;
        _regexRouterReplacement = replacement;
        return this;
    }

    /// <summary>
    /// Enable dynamic routing based on a field in each Kafka record.
    /// Each record must contain the specified field with the target Iceberg table name.
    /// Example: routeField="iceberg_table" expects records like {"iceberg_table": "m3_silver.product", ...}
    /// </summary>
    public IcebergSinkConnectorBuilder WithDynamicRouting(string routeField)
    {
        _dynamicRoutingEnabled = true;
        _routeField = routeField;
        return this;
    }

    /// <summary>
    /// Configure schema registry caching to prevent 404 errors during schema propagation.
    /// </summary>
    /// <param name="cacheSize">Number of schemas to cache (default: 1000)</param>
    /// <param name="cacheTtlMs">Cache TTL in milliseconds (default: 300000 = 5 minutes)</param>
    public IcebergSinkConnectorBuilder WithSchemaRegistryCache(int cacheSize = 1000, int cacheTtlMs = 300000)
    {
        _cacheSize = cacheSize;
        _cacheTtlMs = cacheTtlMs;
        return this;
    }

    /// <summary>
    /// Enable fail-fast mode (errors.tolerance=none) instead of DLQ-based error handling.
    /// Use this when you want the connector to fail immediately on schema errors.
    /// </summary>
    /// <param name="retryDelayMaxMs">Maximum retry delay in ms (default: 60000)</param>
    /// <param name="retryTimeoutMs">Total retry timeout in ms (default: 300000)</param>
    public IcebergSinkConnectorBuilder WithFailFastMode(int retryDelayMaxMs = 60000, int retryTimeoutMs = 300000)
    {
        _failFastMode = true;
        _retryDelayMaxMs = retryDelayMaxMs;
        _retryTimeoutMs = retryTimeoutMs;
        return this;
    }

    /// <summary>
    /// Set custom control topic and group ID prefix for commit coordination.
    /// </summary>
    public IcebergSinkConnectorBuilder WithControlTopic(string controlTopic, string groupIdPrefix)
    {
        _controlTopic = controlTopic;
        _controlGroupIdPrefix = groupIdPrefix;
        return this;
    }

    /// <summary>
    /// Set the commit interval in milliseconds.
    /// Default is 60000ms (1 minute). Use 10000ms (10s) for debug connectors.
    /// </summary>
    public IcebergSinkConnectorBuilder WithCommitInterval(int intervalMs)
    {
        _commitIntervalMs = intervalMs;
        return this;
    }

    /// <summary>
    /// Set the Kafka Connect cluster name (default: "m3-kafka-connect")
    /// </summary>
    public IcebergSinkConnectorBuilder WithClusterName(string clusterName)
    {
        _clusterName = clusterName;
        return this;
    }

    /// <summary>
    /// Set the maximum number of tasks (default: 1)
    /// </summary>
    public IcebergSinkConnectorBuilder WithTasksMax(int tasksMax)
    {
        _tasksMax = tasksMax;
        return this;
    }

    /// <summary>
    /// Override the schema registry URL
    /// </summary>
    public IcebergSinkConnectorBuilder WithSchemaRegistry(string url, string auth)
    {
        _schemaRegistryUrl = url;
        _schemaRegistryAuth = auth;
        return this;
    }

    /// <summary>
    /// Override the DLQ topic name (default: "m3.iceberg.dlq")
    /// </summary>
    public IcebergSinkConnectorBuilder WithDlqTopic(string dlqTopic)
    {
        _dlqTopic = dlqTopic;
        return this;
    }

    /// <summary>
    /// Build and create the KafkaConnector resource.
    /// </summary>
    public ComponentResource Build()
    {
        var componentResource =
            new ComponentResource($"m3-iceberg-sink-{_connectorName}", $"m3-iceberg-sink-{_connectorName}");

        var provider = new Pulumi.Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{_manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = componentResource
        });
        var config = new Dictionary<string, object>
        {
            // ========================================================================
            // 1. SOURCE TOPIC(S)
            // ========================================================================


            // ========================================================================
            // 2. GLOBAL TABLE SETTINGS
            // ========================================================================
            ["iceberg.tables.auto-create-enabled"] = true,
            ["iceberg.tables.upsert-mode-enabled"] = _upsertModeEnabled,
            ["iceberg.tables.evolve-schema-enabled"] = true,
            ["iceberg.tables.schema-force-optional"] = true,

            // ========================================================================
            // 3. ICEBERG CATALOG CONNECTION (Polaris)
            // ========================================================================
            ["iceberg.catalog"] = "iceberg",
            ["iceberg.catalog.type"] = "rest",
            ["iceberg.catalog.catalog-name"] = _catalogName,
            ["iceberg.catalog.uri"] = _polarisUri,
            ["iceberg.catalog.credential"] = "root:${env:POLARIS_PASSWORD}",
            ["iceberg.catalog.warehouse"] = _catalogName,
            ["iceberg.catalog.scope"] = "PRINCIPAL_ROLE:ALL",
            ["iceberg.catalog.oauth2-server-uri"] = $"{_polarisUri}/v1/oauth/tokens",

            // ========================================================================
            // 4. SCHEMA REGISTRY & SERIALIZATION
            // ========================================================================
            ["key.converter"] = "io.confluent.connect.avro.AvroConverter",
            ["key.converter.schema.registry.url"] = _schemaRegistryUrl,
            ["key.converter.schema.registry.basic.auth.user.info"] = _schemaRegistryAuth,
            ["key.converter.schema.registry.basic.auth.credentials.source"] = "USER_INFO",
            ["key.converter.schemas.enable"] = true,

            ["value.converter"] = "io.confluent.connect.avro.AvroConverter",
            ["value.converter.schema.registry.url"] = _schemaRegistryUrl,
            ["value.converter.schema.registry.basic.auth.user.info"] = _schemaRegistryAuth,
            ["value.converter.schema.registry.basic.auth.credentials.source"] = "USER_INFO",
            ["value.converter.schemas.enable"] = true,

            // ========================================================================
            // 5. COMMIT COORDINATION & PERFORMANCE
            // ========================================================================
            ["iceberg.control.commit.interval-ms"] = _commitIntervalMs,
            ["iceberg.control.commit.threads"] = 2,
            ["iceberg.control.commit.timeout-ms"] = 30000,
            ["iceberg.control.topic"] = _controlTopic ?? $"sink-control-iceberg-{_connectorName}",
            ["iceberg.control.group-id-prefix"] = _controlGroupIdPrefix ?? $"m3-iceberg-{_connectorName}-control",

            // ========================================================================
            // 6. ERROR HANDLING (base configuration)
            // ========================================================================
            ["errors.log.enable"] = true,
            ["errors.log.include.messages"] = true,
        };

        // ========================================================================
        // CONDITIONAL CONFIGURATION
        // ========================================================================
        
        // Topic selection: Use regex or explicit topic
        if (!string.IsNullOrEmpty(_topicsRegex))
        {
            config["topics.regex"] = _topicsRegex;
        }
        else if (!string.IsNullOrEmpty(_sourceTopic))
        {
            config["topics"] = _sourceTopic;
        }
        else
        {
            throw new ArgumentException("Either WithSourceTopic or WithTopicsRegex must be specified");
        }

        // Destination table (only for single-topic connectors)
        if (!string.IsNullOrEmpty(_destinationTable))
        {
            config["iceberg.tables"] = _destinationTable;
        }

        // Default ID columns (global strategy for all tables)
        if (!string.IsNullOrEmpty(_defaultIdColumns))
        {
            config["iceberg.tables.default-id-columns"] = _defaultIdColumns;
        }

        // Table-specific ID columns (overrides default)
        if (!string.IsNullOrEmpty(_idColumns) && !string.IsNullOrEmpty(_destinationTable))
        {
            config[$"iceberg.table.{_destinationTable}.id-columns"] = _idColumns;
        }

        // Table-specific partitioning
        if (!string.IsNullOrEmpty(_partitionBy) && !string.IsNullOrEmpty(_destinationTable))
        {
            config[$"iceberg.table.{_destinationTable}.partition-by"] = _partitionBy;
        }

        // RegexRouter SMT for topic-to-table name transformation
        if (!string.IsNullOrEmpty(_regexRouterPattern) && !string.IsNullOrEmpty(_regexRouterReplacement))
        {
            config["transforms"] = "route";
            config["transforms.route.type"] = "org.apache.kafka.connect.transforms.RegexRouter";
            config["transforms.route.regex"] = _regexRouterPattern;
            config["transforms.route.replacement"] = _regexRouterReplacement;
        }

        // Dynamic routing configuration
        if (_dynamicRoutingEnabled && !string.IsNullOrEmpty(_routeField))
        {
            config["iceberg.tables.dynamic-enabled"] = true;
            config["iceberg.tables.route-field"] = _routeField;
        }

        // Schema registry cache configuration
        if (_cacheSize.HasValue)
        {
            config["value.converter.cache.size"] = _cacheSize.Value;
            config["key.converter.cache.size"] = _cacheSize.Value;
        }
        if (_cacheTtlMs.HasValue)
        {
            config["value.converter.cache.ttl.ms"] = _cacheTtlMs.Value;
            config["key.converter.cache.ttl.ms"] = _cacheTtlMs.Value;
        }

        // Error handling configuration
        if (_failFastMode)
        {
            config["errors.tolerance"] = "none";
            if (_retryDelayMaxMs.HasValue)
                config["errors.retry.delay.max.ms"] = _retryDelayMaxMs.Value;
            if (_retryTimeoutMs.HasValue)
                config["errors.retry.timeout.ms"] = _retryTimeoutMs.Value;
        }
        else
        {
            config["errors.tolerance"] = "all";
            config["errors.deadletterqueue.topic.name"] = _dlqTopic;
            config["errors.deadletterqueue.context.headers.enable"] = true;
            config["errors.deadletterqueue.producer.acks"] = "all";
            config["errors.deadletterqueue.producer.enable.idempotence"] = true;
        }

        new KafkaConnector($"m3-iceberg-sink-{_connectorName}", new KafkaConnectorArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = $"m3-iceberg-sink-{_connectorName}",
                Namespace = "kafka-connect",
                Labels = new Dictionary<string, string>
                {
                    { "strimzi.io/cluster", _clusterName }
                }
            },
            Spec = new KafkaConnectorSpecArgs
            {
                Class = "io.tabular.iceberg.connect.IcebergSinkConnector",
                TasksMax = _tasksMax,
                Config = config
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = componentResource
        });

        return componentResource;
    }
}