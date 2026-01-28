using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pulumi;
using Pulumi.Crds.KafkaConnect;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

/// <summary>
/// Fluent builder for creating Debezium source connectors.
/// Supports PostgreSQL, DB2, MySQL, SQL Server, and MongoDB connectors.
/// </summary>
public class DebeziumSourceConnectorBuilder
{
    // ========================================================================
    // PRIVATE FIELDS
    // ========================================================================

    private readonly string _manifestsRoot;

    // Connector identity
    private string _connectorName = "";
    private string _connectorClass = "";
    private string _clusterName = "m3-kafka-connect";
    private int _tasksMax = 1;

    // Database connection
    private string _databaseHostname = "";
    private string _databasePort = "";
    private string _databaseUser = "";
    private string _databasePassword = "";
    private string _databaseName = "";

    // Debezium core configuration
    private string _topicPrefix = "";
    private string _snapshotMode = "initial";
    private string? _tableIncludeList;
    private string? _tableExcludeList;

    // PostgreSQL-specific
    private string? _pluginName;
    private string? _publicationName;
    private string? _slotName;

    // SMT configuration
    private bool _enableUnwrap = false;
    private bool _dropTombstones = true;
    private string _deleteHandlingMode = "rewrite";
    private string _addFields = "op,source.ts_ms";
    private string _addHeaders = "op,source.ts_ms";

    private bool _enableTopicRouting = false;
    private string? _routeRegex;
    private string? _routeReplacement;

    // Schema Registry
    private string _schemaRegistryUrl =
        "http://warpstream-schema-registry-warpstream-agent.warpstream.svc.cluster.local:9094";
    private string _schemaRegistryAuth = "${env:SCHEMA_REGISTRY_USERNAME}:${env:SCHEMA_REGISTRY_PASSWORD}";
    private bool _useAvroConverter = false;
    private bool _useJsonConverter = false;

    // Error handling
    private bool _failFastMode = false;
    private string? _dlqTopic;

    // Performance tuning
    private int _maxBatchSize = 2048;
    private int _maxQueueSize = 8192;
    private int _pollIntervalMs = 1000;

    // DD130 Naming Convention fields
    private NamingConventionHelper.DataLayer? _layer;
    private string? _domain;
    private string? _subdomain;
    private string? _dataset;
    private string? _processingStage;
    private string? _environment;

    // Database type for preset validation
    private string? _databaseType;

    // ========================================================================
    // CONSTRUCTOR
    // ========================================================================

    public DebeziumSourceConnectorBuilder(string manifestsRoot)
    {
        _manifestsRoot = manifestsRoot;
    }

    // ========================================================================
    // DATABASE PRESET METHODS
    // ========================================================================

    /// <summary>
    /// Configures the builder for a PostgreSQL Debezium connector.
    /// Sets the connector class and default PostgreSQL-specific settings.
    /// </summary>
    public DebeziumSourceConnectorBuilder ForPostgres()
    {
        _connectorClass = "io.debezium.connector.postgresql.PostgresConnector";
        _databaseType = "postgres";
        _pluginName = "pgoutput"; // Default PostgreSQL output plugin
        return this;
    }

    /// <summary>
    /// Configures the builder for a DB2 Debezium connector.
    /// Sets the connector class and default DB2-specific settings.
    /// </summary>
    public DebeziumSourceConnectorBuilder ForDb2()
    {
        _connectorClass = "io.debezium.connector.db2.Db2Connector";
        _databaseType = "db2";
        return this;
    }

    /// <summary>
    /// Configures the builder for a MySQL Debezium connector.
    /// Sets the connector class and default MySQL-specific settings.
    /// </summary>
    public DebeziumSourceConnectorBuilder ForMySql()
    {
        _connectorClass = "io.debezium.connector.mysql.MySqlConnector";
        _databaseType = "mysql";
        return this;
    }

    /// <summary>
    /// Configures the builder for a SQL Server Debezium connector.
    /// Sets the connector class and default SQL Server-specific settings.
    /// </summary>
    public DebeziumSourceConnectorBuilder ForSqlServer()
    {
        _connectorClass = "io.debezium.connector.sqlserver.SqlServerConnector";
        _databaseType = "sqlserver";
        return this;
    }

    /// <summary>
    /// Configures the builder for a MongoDB Debezium connector.
    /// Sets the connector class and default MongoDB-specific settings.
    /// </summary>
    public DebeziumSourceConnectorBuilder ForMongoDB()
    {
        _connectorClass = "io.debezium.connector.mongodb.MongoDbConnector";
        _databaseType = "mongodb";
        return this;
    }

    // ========================================================================
    // COMMON CONFIGURATION METHODS
    // ========================================================================

    /// <summary>
    /// Sets the connector name (Kubernetes resource name).
    /// </summary>
    public DebeziumSourceConnectorBuilder WithConnectorName(string connectorName)
    {
        _connectorName = connectorName;
        return this;
    }

    /// <summary>
    /// Sets the Kafka Connect cluster name (Strimzi cluster label).
    /// </summary>
    public DebeziumSourceConnectorBuilder WithClusterName(string clusterName)
    {
        _clusterName = clusterName;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of tasks for the connector.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithTasksMax(int tasksMax)
    {
        _tasksMax = tasksMax;
        return this;
    }

    // ========================================================================
    // DATABASE CONNECTION METHODS
    // ========================================================================

    /// <summary>
    /// Sets database connection parameters explicitly.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithDatabaseConnection(
        string hostname,
        string port,
        string user,
        string password,
        string database)
    {
        _databaseHostname = hostname;
        _databasePort = port;
        _databaseUser = user;
        _databasePassword = password;
        _databaseName = database;
        return this;
    }

    /// <summary>
    /// Uses environment variables for database connection.
    /// Uses POSTGRES_* variables by default, or database-specific variables if a preset was used.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithDatabaseConnectionFromEnv(string? prefix = null)
    {
        var envPrefix = prefix ?? _databaseType?.ToUpperInvariant() ?? "POSTGRES";
        _databaseHostname = $"${{env:{envPrefix}_HOST}}";
        _databasePort = $"${{env:{envPrefix}_PORT}}";
        _databaseUser = $"${{env:{envPrefix}_USER}}";
        _databasePassword = $"${{env:{envPrefix}_PASSWORD}}";
        _databaseName = $"${{env:{envPrefix}_DB}}";
        return this;
    }

    // ========================================================================
    // DEBEZIUM CONFIGURATION METHODS
    // ========================================================================

    /// <summary>
    /// Sets the topic prefix for change data capture topics.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithTopicPrefix(string prefix)
    {
        _topicPrefix = prefix;
        return this;
    }

    /// <summary>
    /// Sets the snapshot mode (initial, always, never, schema_only, etc.).
    /// </summary>
    public DebeziumSourceConnectorBuilder WithSnapshotMode(string mode)
    {
        _snapshotMode = mode;
        return this;
    }

    /// <summary>
    /// Sets the list of tables to include in CDC.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithTableIncludeList(params string[] tables)
    {
        _tableIncludeList = string.Join(",", tables);
        return this;
    }

    /// <summary>
    /// Sets the list of tables to exclude from CDC.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithTableExcludeList(params string[] tables)
    {
        _tableExcludeList = string.Join(",", tables);
        return this;
    }

    // ========================================================================
    // POSTGRESQL-SPECIFIC METHODS
    // ========================================================================

    /// <summary>
    /// Sets the PostgreSQL replication slot name.
    /// Only applicable for PostgreSQL connectors.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithReplicationSlot(string slotName)
    {
        _slotName = slotName;
        return this;
    }

    /// <summary>
    /// Sets the PostgreSQL publication name.
    /// Only applicable for PostgreSQL connectors.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithPublication(string publicationName)
    {
        _publicationName = publicationName;
        return this;
    }

    /// <summary>
    /// Sets the PostgreSQL logical decoding output plugin (pgoutput, decoderbufs).
    /// Only applicable for PostgreSQL connectors.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithPlugin(string pluginName)
    {
        _pluginName = pluginName;
        return this;
    }

    // ========================================================================
    // SMT CONFIGURATION METHODS
    // ========================================================================

    /// <summary>
    /// Enables the ExtractNewRecordState SMT to unwrap Debezium change events.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithUnwrap(
        bool dropTombstones = true,
        string deleteHandlingMode = "rewrite",
        string addFields = "op,source.ts_ms",
        string addHeaders = "op,source.ts_ms")
    {
        _enableUnwrap = true;
        _dropTombstones = dropTombstones;
        _deleteHandlingMode = deleteHandlingMode;
        _addFields = addFields;
        _addHeaders = addHeaders;
        return this;
    }

    /// <summary>
    /// Enables topic routing using RegexRouter SMT.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithTopicRouting(string regex, string replacement)
    {
        _enableTopicRouting = true;
        _routeRegex = regex;
        _routeReplacement = replacement;
        return this;
    }

    // ========================================================================
    // SCHEMA REGISTRY METHODS
    // ========================================================================

    /// <summary>
    /// Sets the Schema Registry URL and authentication.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithSchemaRegistry(string url, string? auth = null)
    {
        _schemaRegistryUrl = url;
        if (auth != null)
        {
            _schemaRegistryAuth = auth;
        }
        return this;
    }

    /// <summary>
    /// Enables Avro serialization with Schema Registry.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithAvroConverter()
    {
        _useAvroConverter = true;
        _useJsonConverter = false;
        return this;
    }

    /// <summary>
    /// Enables JSON serialization (with or without schemas).
    /// </summary>
    public DebeziumSourceConnectorBuilder WithJsonConverter(bool schemasEnable = false)
    {
        _useJsonConverter = true;
        _useAvroConverter = false;
        return this;
    }

    // ========================================================================
    // ERROR HANDLING METHODS
    // ========================================================================

    /// <summary>
    /// Enables fail-fast mode - connector will stop on first error.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithFailFastMode()
    {
        _failFastMode = true;
        return this;
    }

    /// <summary>
    /// Sets the Dead Letter Queue topic for error handling.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithDlqTopic(string topic)
    {
        _dlqTopic = topic;
        _failFastMode = false; // DLQ implies tolerance
        return this;
    }

    // ========================================================================
    // PERFORMANCE TUNING METHODS
    // ========================================================================

    /// <summary>
    /// Sets performance tuning parameters.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithPerformanceTuning(
        int maxBatchSize = 2048,
        int maxQueueSize = 8192,
        int pollIntervalMs = 1000)
    {
        _maxBatchSize = maxBatchSize;
        _maxQueueSize = maxQueueSize;
        _pollIntervalMs = pollIntervalMs;
        return this;
    }

    // ========================================================================
    // DD130 NAMING CONVENTION METHODS
    // ========================================================================

    /// <summary>
    /// Sets DD130 naming convention components.
    /// Auto-derives connector name, topic prefix, and DLQ topic.
    /// </summary>
    public DebeziumSourceConnectorBuilder WithNaming(
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

    // ========================================================================
    // BUILD METHOD
    // ========================================================================

    /// <summary>
    /// Builds the Debezium source connector Pulumi resource.
    /// </summary>
    public ComponentResource Build()
    {
        // Validate required configuration
        if (string.IsNullOrEmpty(_connectorClass))
        {
            throw new InvalidOperationException(
                "Connector class not set. Use ForPostgres(), ForDb2(), ForMySql(), ForSqlServer(), or ForMongoDB().");
        }

        // ========================================================================
        // DD130 NAMING: Derive names from components if WithNaming() was used
        // ========================================================================
        if (_layer.HasValue && !string.IsNullOrEmpty(_domain) && !string.IsNullOrEmpty(_dataset))
        {
            // Auto-derive topic prefix if not explicitly set
            if (string.IsNullOrEmpty(_topicPrefix))
            {
                _topicPrefix = $"{_domain}-cdc";
            }

            // Build TopicComponents for naming derivation
            var topicComponents = new NamingConventionHelper.TopicComponents(
                _environment, _layer.Value, _domain, _subdomain, _dataset, _processingStage);

            // Auto-derive connector name if not explicitly set
            if (string.IsNullOrEmpty(_connectorName))
            {
                _connectorName = NamingConventionHelper.ToConnectorName(topicComponents);
            }

            // Auto-derive DLQ topic if not explicitly set
            if (string.IsNullOrEmpty(_dlqTopic) && !_failFastMode)
            {
                var baseTopic = NamingConventionHelper.ToTopicName(topicComponents);
                _dlqTopic = NamingConventionHelper.ToDlqTopic(baseTopic);
            }
        }

        // Validate connector name
        if (string.IsNullOrEmpty(_connectorName))
        {
            throw new InvalidOperationException(
                "Connector name not set. Use WithConnectorName() or WithNaming().");
        }

        // Create component resource
        var componentResource = new ComponentResource(
            $"debezium-{_connectorName}",
            $"debezium-{_connectorName}");

        // Create Pulumi provider for YAML rendering
        var provider = new Pulumi.Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{_manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = componentResource
        });

        // ========================================================================
        // BUILD CONFIGURATION
        // ========================================================================
        var config = new Dictionary<string, object>();

        // Database connection
        if (!string.IsNullOrEmpty(_databaseHostname))
        {
            config["database.hostname"] = _databaseHostname;
            config["database.port"] = _databasePort;
            config["database.user"] = _databaseUser;
            config["database.password"] = _databasePassword;
            config["database.dbname"] = _databaseName;
        }

        // Connector identity
        if (!string.IsNullOrEmpty(_topicPrefix))
        {
            config["topic.prefix"] = _topicPrefix;
        }

        // Snapshot mode
        config["snapshot.mode"] = _snapshotMode;

        // Table selection
        if (!string.IsNullOrEmpty(_tableIncludeList))
        {
            config["table.include.list"] = _tableIncludeList;
        }
        if (!string.IsNullOrEmpty(_tableExcludeList))
        {
            config["table.exclude.list"] = _tableExcludeList;
        }

        // PostgreSQL-specific configuration
        if (_databaseType == "postgres")
        {
            if (!string.IsNullOrEmpty(_pluginName))
            {
                config["plugin.name"] = _pluginName;
            }
            if (!string.IsNullOrEmpty(_publicationName))
            {
                config["publication.name"] = _publicationName;
            }
            if (!string.IsNullOrEmpty(_slotName))
            {
                config["slot.name"] = _slotName;
            }
        }

        // ========================================================================
        // SMT CHAIN
        // ========================================================================
        var transforms = new List<string>();

        if (_enableUnwrap)
        {
            transforms.Add("unwrap");
            config["transforms.unwrap.type"] = "io.debezium.transforms.ExtractNewRecordState";
            config["transforms.unwrap.add.fields"] = _addFields;
            config["transforms.unwrap.add.headers"] = _addHeaders;
            config["transforms.unwrap.delete.handling.mode"] = _deleteHandlingMode;
            config["transforms.unwrap.drop.tombstones"] = _dropTombstones;
        }

        if (_enableTopicRouting && !string.IsNullOrEmpty(_routeRegex))
        {
            transforms.Add("route");
            config["transforms.route.type"] = "org.apache.kafka.connect.transforms.RegexRouter";
            config["transforms.route.regex"] = _routeRegex;
            config["transforms.route.replacement"] = _routeReplacement!;
        }

        if (transforms.Count > 0)
        {
            config["transforms"] = string.Join(",", transforms);
        }

        // ========================================================================
        // SCHEMA REGISTRY & SERIALIZATION
        // ========================================================================
        if (_useAvroConverter)
        {
            config["key.converter"] = "io.confluent.connect.avro.AvroConverter";
            config["key.converter.schema.registry.url"] = _schemaRegistryUrl;
            config["key.converter.schema.registry.basic.auth.user.info"] = _schemaRegistryAuth;
            config["key.converter.schema.registry.basic.auth.credentials.source"] = "USER_INFO";
            config["key.converter.schemas.enable"] = true;

            config["value.converter"] = "io.confluent.connect.avro.AvroConverter";
            config["value.converter.schema.registry.url"] = _schemaRegistryUrl;
            config["value.converter.schema.registry.basic.auth.user.info"] = _schemaRegistryAuth;
            config["value.converter.schema.registry.basic.auth.credentials.source"] = "USER_INFO";
            config["value.converter.schemas.enable"] = true;
        }
        else if (_useJsonConverter)
        {
            config["key.converter"] = "org.apache.kafka.connect.json.JsonConverter";
            config["key.converter.schemas.enable"] = false;

            config["value.converter"] = "org.apache.kafka.connect.json.JsonConverter";
            config["value.converter.schemas.enable"] = false;
        }

        // ========================================================================
        // ERROR HANDLING
        // ========================================================================
        if (_failFastMode)
        {
            config["errors.tolerance"] = "none";
        }
        else
        {
            config["errors.tolerance"] = "all";
            if (!string.IsNullOrEmpty(_dlqTopic))
            {
                config["errors.deadletterqueue.topic.name"] = _dlqTopic;
                config["errors.deadletterqueue.context.headers.enable"] = true;
            }
        }

        // ========================================================================
        // PERFORMANCE TUNING
        // ========================================================================
        config["max.batch.size"] = _maxBatchSize;
        config["max.queue.size"] = _maxQueueSize;
        config["poll.interval.ms"] = _pollIntervalMs;

        // ========================================================================
        // CREATE KAFKA CONNECTOR RESOURCE
        // ========================================================================
        new KafkaConnector($"debezium-{_connectorName}", new KafkaConnectorArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = _connectorName,
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
                Class = _connectorClass,
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

    // ========================================================================
    // PRIVATE HELPERS
    // ========================================================================

    private static string ComputeConfigHash(Dictionary<string, object> config)
    {
        var json = JsonSerializer.Serialize(config);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
