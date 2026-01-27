using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pulumi;
using Pulumi.Crds.KafkaConnect;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

public class IcebergSinkConnectorBuilder
{
    private string _manifestsRoot = "";
    private string _connectorName = "";
    private string _connectorPrefix = "m3-iceberg-sink";
    private string _sourceTopic = "";
    private string? _topicsRegex;
    private string _destinationTable = "";
    private string? _idColumns;
    private string? _defaultIdColumns;
    private string? _partitionBy;
    private string _clusterName = "m3-kafka-connect";
    private int _tasksMax = 1;
    private bool _upsertModeEnabled = true;

    // DD130 Naming Convention fields
    private NamingConventionHelper.DataLayer? _layer;
    private string? _domain;
    private string? _subdomain;
    private string? _dataset;
    private string? _processingStage;
    private string? _environment;
    private NamingConventionHelper.SchemaCompatibility? _schemaCompatibility;
    private bool _autoSetCompatibility = true;

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

    public IcebergSinkConnectorBuilder(string manifestsRoot)
    {
        _manifestsRoot = manifestsRoot;
    }

    public IcebergSinkConnectorBuilder WithConnectorName(string connectorName)
    {
        _connectorName = connectorName;
        return this;
    }

    public IcebergSinkConnectorBuilder WithSourceTopic(string sourceTopic)
    {
        _sourceTopic = sourceTopic;
        _topicsRegex = null; // Clear regex if explicit topic is set
        return this;
    }

    public IcebergSinkConnectorBuilder WithTopicsRegex(string topicsRegex)
    {
        _topicsRegex = topicsRegex;
        _sourceTopic = ""; // Clear explicit topic if regex is set
        return this;
    }

    public IcebergSinkConnectorBuilder WithDestinationTable(string destinationTable)
    {
        _destinationTable = destinationTable;
        return this;
    }

    public IcebergSinkConnectorBuilder WithIdColumns(params string[] idColumns)
    {
        _idColumns = string.Join(",", idColumns);
        return this;
    }

    public IcebergSinkConnectorBuilder WithDefaultIdColumns(string defaultIdColumns)
    {
        _defaultIdColumns = defaultIdColumns;
        return this;
    }

    public IcebergSinkConnectorBuilder WithPartitionBy(string partitionColumn)
    {
        _partitionBy = partitionColumn;
        return this;
    }

    public IcebergSinkConnectorBuilder WithUpsertMode(bool enabled)
    {
        _upsertModeEnabled = enabled;
        return this;
    }

    public IcebergSinkConnectorBuilder WithRegexRouter(string pattern, string replacement)
    {
        _regexRouterPattern = pattern;
        _regexRouterReplacement = replacement;
        return this;
    }

    public IcebergSinkConnectorBuilder WithDynamicRouting(string routeField)
    {
        _dynamicRoutingEnabled = true;
        _routeField = routeField;
        return this;
    }

    public IcebergSinkConnectorBuilder WithSchemaRegistryCache(int cacheSize = 1000, int cacheTtlMs = 300000)
    {
        _cacheSize = cacheSize;
        _cacheTtlMs = cacheTtlMs;
        return this;
    }

    public IcebergSinkConnectorBuilder WithFailFastMode(int retryDelayMaxMs = 60000, int retryTimeoutMs = 300000)
    {
        _failFastMode = true;
        _retryDelayMaxMs = retryDelayMaxMs;
        _retryTimeoutMs = retryTimeoutMs;
        return this;
    }

    public IcebergSinkConnectorBuilder WithControlTopic(string controlTopic, string groupIdPrefix)
    {
        _controlTopic = controlTopic;
        _controlGroupIdPrefix = groupIdPrefix;
        return this;
    }

    public IcebergSinkConnectorBuilder WithCommitInterval(int intervalMs)
    {
        _commitIntervalMs = intervalMs;
        return this;
    }

    public IcebergSinkConnectorBuilder WithConnectorPrefix(string prefix)
    {
        _connectorPrefix = prefix;
        return this;
    }

    public IcebergSinkConnectorBuilder WithClusterName(string clusterName)
    {
        _clusterName = clusterName;
        return this;
    }

    public IcebergSinkConnectorBuilder WithTasksMax(int tasksMax)
    {
        _tasksMax = tasksMax;
        return this;
    }

    public IcebergSinkConnectorBuilder WithSchemaRegistry(string url, string auth)
    {
        _schemaRegistryUrl = url;
        _schemaRegistryAuth = auth;
        return this;
    }

    public IcebergSinkConnectorBuilder WithDlqTopic(string dlqTopic)
    {
        _dlqTopic = dlqTopic;
        return this;
    }

    public IcebergSinkConnectorBuilder WithNaming(
        NamingConventionHelper.DataLayer layer,
        string domain,
        string dataset,
        string? subdomain = null,
        string? processingStage = null,
        string? environment = null)
    {
        _layer = layer;
        _domain = domain;
        _dataset = dataset;
        _subdomain = subdomain;
        _processingStage = processingStage;
        _environment = environment;
        return this;
    }

    public IcebergSinkConnectorBuilder WithSchemaCompatibility(NamingConventionHelper.SchemaCompatibility compatibility)
    {
        _schemaCompatibility = compatibility;
        return this;
    }

    public IcebergSinkConnectorBuilder WithAutoSetCompatibility(bool enabled)
    {
        _autoSetCompatibility = enabled;
        return this;
    }

    public ComponentResource Build()
    {
        // ========================================================================
        // DD130 NAMING: Derive names from components if WithNaming() was used
        // ========================================================================
        NamingConventionHelper.TopicComponents? parsedComponents = null;

        if (_layer.HasValue && !string.IsNullOrEmpty(_domain) && !string.IsNullOrEmpty(_dataset))
        {
            // Build from explicit DD130 components
            parsedComponents = new NamingConventionHelper.TopicComponents(
                _environment, _layer.Value, _domain, _subdomain, _dataset, _processingStage);

            // Auto-derive topic, table, connector name, and DLQ if not explicitly set
            if (string.IsNullOrEmpty(_sourceTopic) && string.IsNullOrEmpty(_topicsRegex))
            {
                _sourceTopic = NamingConventionHelper.ToTopicName(parsedComponents);
            }
            if (string.IsNullOrEmpty(_destinationTable))
            {
                _destinationTable = NamingConventionHelper.ToIcebergTable(parsedComponents);
            }
            if (string.IsNullOrEmpty(_connectorName))
            {
                // Sanitize for Kubernetes RFC 1123: replace underscores with hyphens
                var sanitizedDataset = _dataset!.Replace("_", "-");
                var sanitizedStage = _processingStage?.Replace("_", "-");
                _connectorName = $"{sanitizedDataset}{(sanitizedStage != null ? $"-{sanitizedStage}" : "")}";
            }
            // Update connector prefix to follow DD130 pattern
            _connectorPrefix = $"{_layer.Value.ToString().ToLowerInvariant()}-{_domain}";
        }
        else if (!string.IsNullOrEmpty(_sourceTopic))
        {
            // Parse from existing topic name to derive components
            try
            {
                parsedComponents = NamingConventionHelper.ParseTopic(_sourceTopic);

                // Auto-derive destination table if not set
                if (string.IsNullOrEmpty(_destinationTable))
                {
                    _destinationTable = NamingConventionHelper.ToIcebergTable(parsedComponents);
                }
            }
            catch (ArgumentException)
            {
                // Topic doesn't follow DD130 format, continue with explicit values
            }
        }

        // Determine schema compatibility (explicit > auto-derived from layer > null)
        var effectiveCompatibility = _schemaCompatibility
            ?? (parsedComponents != null
                ? NamingConventionHelper.GetDefaultCompatibility(parsedComponents.Layer)
                : (NamingConventionHelper.SchemaCompatibility?)null);

        // Auto-derive DLQ topic if not explicitly set and using explicit topic
        var effectiveDlqTopic = !string.IsNullOrEmpty(_dlqTopic) && _dlqTopic != "m3.iceberg.dlq"
            ? _dlqTopic
            : (!string.IsNullOrEmpty(_sourceTopic)
                ? NamingConventionHelper.ToDlqTopic(_sourceTopic)
                : _dlqTopic);

        var componentResource =
            new ComponentResource($"{_connectorPrefix}-{_connectorName}", $"{_connectorPrefix}-{_connectorName}");

        var provider = new Pulumi.Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{_manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = componentResource
        });

        // Create PreSync schema check job if using explicit topic (not regex)
        if (!string.IsNullOrEmpty(_sourceTopic))
        {
            var schemaJobBuilder = new SchemaValidationJobBuilder()
                .WithProvider(provider)
                .WithParent(componentResource)
                .WithJobName(_connectorName)
                .WithSchemaSubject($"{_sourceTopic}-value")
                .WithSchemaRegistryUrl(_schemaRegistryUrl)
                .WithAutoSetCompatibility(_autoSetCompatibility);

            // Set compatibility if determined
            if (effectiveCompatibility.HasValue)
            {
                schemaJobBuilder.WithSchemaCompatibility(effectiveCompatibility.Value);
            }

            schemaJobBuilder.Build();
        }

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
            ["iceberg.control.topic"] = _controlTopic ?? $"sink-control-{_connectorPrefix}-{_connectorName}",
            ["iceberg.control.group-id-prefix"] = _controlGroupIdPrefix ?? $"{_connectorPrefix}-{_connectorName}-control",

            // ========================================================================
            // 6. ERROR HANDLING (base configuration)
            // ========================================================================
            // Note: Idempotent producer is enabled at cluster level (KafkaConnectClusterBuilder)
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
            config["errors.deadletterqueue.topic.name"] = effectiveDlqTopic;
            config["errors.deadletterqueue.context.headers.enable"] = true;
            config["errors.deadletterqueue.producer.acks"] = "all";
            config["errors.deadletterqueue.producer.enable.idempotence"] = true;
        }

        new KafkaConnector($"{_connectorPrefix}-{_connectorName}", new KafkaConnectorArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = $"{_connectorPrefix}-{_connectorName}",
                Namespace = "kafka-connect",
                Labels = new Dictionary<string, string>
                {
                    { "strimzi.io/cluster", _clusterName }
                },
                Annotations = new Dictionary<string, string>
                {
                    { "config-hash", ComputeConfigHash(config) }
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

    private static string ComputeConfigHash(Dictionary<string, object> config)
    {
        var json = JsonSerializer.Serialize(config);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}