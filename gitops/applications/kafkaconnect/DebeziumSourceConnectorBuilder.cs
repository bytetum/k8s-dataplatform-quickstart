using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
namespace applications.kafkaconnect;
/// <summary>
/// Supported database types for Debezium CDC connectors.
/// </summary>
public enum DatabaseType
{
    Postgres,
    Db2,
    MySQL,
    SqlServer,
    Oracle,
    MongoDB
}
/// <summary>
/// Snapshot modes for Debezium connectors.
/// </summary>
public enum SnapshotMode
{
    /// <summary>Initial snapshot on first run, then streaming only.</summary>
    Initial,
    /// <summary>Always perform snapshot on connector start.</summary>
    Always,
    /// <summary>Never perform snapshot, start from current position.</summary>
    Never,
    /// <summary>Snapshot schema only, no data.</summary>
    SchemaOnly,
    /// <summary>Perform snapshot only when no offset is found.</summary>
    WhenNeeded
}
/// <summary>
/// Delete handling modes for ExtractNewRecordState SMT.
/// </summary>
public enum DeleteHandlingMode
{
    /// <summary>No special handling for deletes.</summary>
    None,
    /// <summary>Rewrite delete events with __deleted field.</summary>
    Rewrite,
    /// <summary>Drop delete events entirely.</summary>
    Drop
}
/// <summary>
/// Fluent builder for creating Debezium source connectors.
/// Supports multiple database types and follows DD130 naming conventions.
/// </summary>
public class DebeziumSourceConnectorBuilder
{
    // Core configuration
    private readonly string _manifestsRoot;
    private DatabaseType? _databaseType;
    // Database connection
    private string? _databaseHostname;
    private string? _databasePort;
    private string? _databaseUser;
    private string? _databasePassword;
    private string? _databaseName;
    // Connector identity
    private string? _connectorName;
    private string? _topicPrefix;
    private string _clusterName = "m3-kafka-connect";
    private int _tasksMax = 1;
    // DD130 Naming Convention fields
    private NamingConventionHelper.DataLayer? _layer;
    private string? _domain;
    private string? _subdomain;
    private string? _dataset;
    private string? _processingStage;
    private string? _environment;
    // Table selection
    private string? _tableIncludeList;
    private string? _tableExcludeList;
    // Snapshot configuration
    private SnapshotMode _snapshotMode = SnapshotMode.Initial;
    // PostgreSQL-specific configuration
    private string? _postgresPluginName;
    private string? _postgresPublicationName;
    private string? _postgresSlotName;
    // DB2-specific configuration
    private string? _db2AsnProgram;
    private string? _db2AsnLib;
    // MySQL-specific configuration
    private int? _mysqlServerId;
    // MongoDB-specific configuration
    private string? _mongoDbConnectionString;
    // Schema Registry configuration
    private string _schemaRegistryUrl =
        "http://warpstream-schema-registry-warpstream-agent.warpstream.svc.cluster.local:9094";
    private string _schemaRegistryAuth = "${env:SCHEMA_REGISTRY_USERNAME}:${env:SCHEMA_REGISTRY_PASSWORD}";
    // Transform configuration
    private bool _unwrapTransformEnabled = false;
    private DeleteHandlingMode _deleteHandlingMode = DeleteHandlingMode.Rewrite;
    private bool _unwrapAddFields = true;
    private bool _dropTombstones = true;
    private string? _routeTransformRegex;
    private string? _routeTransformReplacement;
    // Converter configuration
    private bool _useAvroConverter = true;
    private bool _jsonSchemasEnable = false;
    // Error handling configuration
    private bool _errorToleranceAll = true;
    private string? _dlqTopicName;
    // Performance tuning
    private int _maxBatchSize = 2048;
    private int _maxQueueSize = 8192;
    private int _pollIntervalMs = 1000;
    /// <summary>
    /// Creates a new Debezium source connector builder.
    /// </summary>
    /// <param name="manifestsRoot">Root directory for generated Kubernetes manifests.</param>
    public DebeziumSourceConnectorBuilder(string manifestsRoot)
    {
        _manifestsRoot = manifestsRoot ?? throw new ArgumentNullException(nameof(manifestsRoot));
    }
    /// <summary>
    /// Sets the database type for the connector.
    /// </summary>
    /// <param name="databaseType">The type of database to connect to.</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithDatabaseType(DatabaseType databaseType)
    {
        _databaseType = databaseType;
        return this;
    }
    /// <summary>
    /// Configures database connection parameters.
    /// </summary>
    /// <param name="hostname">Database hostname (can use ${env:VAR} syntax).</param>
    /// <param name="port">Database port (can use ${env:VAR} syntax).</param>
    /// <param name="user">Database username (can use ${env:VAR} syntax).</param>
    /// <param name="password">Database password (can use ${env:VAR} syntax).</param>
    /// <param name="database">Database name (can use ${env:VAR} syntax).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithDatabaseConnection(
        string hostname,
        string port,
        string user,
        string password,
        string database)
    {
        _databaseHostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        _databasePort = port ?? throw new ArgumentNullException(nameof(port));
        _databaseUser = user ?? throw new ArgumentNullException(nameof(user));
        _databasePassword = password ?? throw new ArgumentNullException(nameof(password));
        _databaseName = database ?? throw new ArgumentNullException(nameof(database));
        return this;
    }
    /// <summary>
    /// Gets the Debezium connector class name for the configured database type.
    /// </summary>
    private string GetConnectorClass()
    {
        return _databaseType switch
        {
            DatabaseType.Postgres => "io.debezium.connector.postgresql.PostgresConnector",
            DatabaseType.Db2 => "io.debezium.connector.db2.Db2Connector",
            DatabaseType.MySQL => "io.debezium.connector.mysql.MySqlConnector",
            DatabaseType.SqlServer => "io.debezium.connector.sqlserver.SqlServerConnector",
            DatabaseType.Oracle => "io.debezium.connector.oracle.OracleConnector",
            DatabaseType.MongoDB => "io.debezium.connector.mongodb.MongoDbConnector",
            null => throw new InvalidOperationException("Database type must be set using WithDatabaseType()"),
            _ => throw new ArgumentOutOfRangeException(nameof(_databaseType), _databaseType, "Unsupported database type")
        };
    }
    /// <summary>
    /// Gets the Debezium snapshot mode string for the configured snapshot mode.
    /// </summary>
    private string GetSnapshotModeString()
    {
        return _snapshotMode switch
        {
            SnapshotMode.Initial => "initial",
            SnapshotMode.Always => "always",
            SnapshotMode.Never => "never",
            SnapshotMode.SchemaOnly => "schema_only",
            SnapshotMode.WhenNeeded => "when_needed",
            _ => "initial"
        };
    }
    /// <summary>
    /// Gets the delete handling mode string for ExtractNewRecordState SMT.
    /// </summary>
    private string GetDeleteHandlingModeString()
    {
        return _deleteHandlingMode switch
        {
            DeleteHandlingMode.None => "none",
            DeleteHandlingMode.Rewrite => "rewrite",
            DeleteHandlingMode.Drop => "drop",
            _ => "rewrite"
        };
    }
    /// <summary>
    /// Configures DD130-compliant naming convention.
    /// Auto-derives topic prefix, connector name, and DLQ topic from the naming components.
    /// </summary>
    /// <param name="layer">Data layer (Bronze, Silver, Gold).</param>
    /// <param name="domain">Business domain (e.g., "m3", "erp").</param>
    /// <param name="dataset">Dataset identifier (e.g., "cdc", "orders").</param>
    /// <param name="subdomain">Optional subdomain for nested organization.</param>
    /// <param name="processingStage">Optional processing stage (raw, cleaned, enriched, etc.).</param>
    /// <param name="environment">Optional environment prefix (dev, staging, prod).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithNaming(
        NamingConventionHelper.DataLayer layer,
        string domain,
        string dataset,
        string? subdomain = null,
        string? processingStage = null,
        string? environment = null)
    {
        _layer = layer;
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
        _subdomain = subdomain;
        _processingStage = processingStage;
        _environment = environment;
        return this;
    }
    /// <summary>
    /// Sets the Debezium topic prefix explicitly.
    /// This overrides any auto-derived prefix from WithNaming().
    /// </summary>
    /// <param name="topicPrefix">The topic prefix for CDC events.</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithTopicPrefix(string topicPrefix)
    {
        _topicPrefix = topicPrefix ?? throw new ArgumentNullException(nameof(topicPrefix));
        return this;
    }
    /// <summary>
    /// Sets the connector name explicitly.
    /// This overrides any auto-derived name from WithNaming().
    /// </summary>
    /// <param name="connectorName">The Kubernetes resource name for the connector.</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithConnectorName(string connectorName)
    {
        _connectorName = connectorName ?? throw new ArgumentNullException(nameof(connectorName));
        return this;
    }
    /// <summary>
    /// Sets the list of tables to include in CDC capture.
    /// Format: "schema.table" (e.g., "public.users", "dbo.orders").
    /// </summary>
    /// <param name="tables">Tables to include (supports wildcards like "public.*").</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithTableIncludeList(params string[] tables)
    {
        if (tables == null || tables.Length == 0)
            throw new ArgumentException("At least one table must be specified", nameof(tables));
        _tableIncludeList = string.Join(",", tables);
        return this;
    }
    /// <summary>
    /// Sets the list of tables to exclude from CDC capture.
    /// Format: "schema.table" (e.g., "public.audit_log").
    /// </summary>
    /// <param name="tables">Tables to exclude (supports wildcards).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithTableExcludeList(params string[] tables)
    {
        if (tables == null || tables.Length == 0)
            throw new ArgumentException("At least one table must be specified", nameof(tables));
        _tableExcludeList = string.Join(",", tables);
        return this;
    }
    /// <summary>
    /// Sets the snapshot mode for initial data capture.
    /// </summary>
    /// <param name="mode">Snapshot mode (Initial, Always, Never, SchemaOnly, WhenNeeded).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithSnapshotMode(SnapshotMode mode)
    {
        _snapshotMode = mode;
        return this;
    }
    /// <summary>
    /// Sets the Strimzi Kafka Connect cluster name for the connector.
    /// </summary>
    /// <param name="clusterName">The cluster name (used in strimzi.io/cluster label).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithClusterName(string clusterName)
    {
        _clusterName = clusterName ?? throw new ArgumentNullException(nameof(clusterName));
        return this;
    }
    /// <summary>
    /// Sets the maximum number of tasks for the connector.
    /// For source connectors, this is typically 1 for consistency.
    /// </summary>
    /// <param name="tasksMax">Maximum number of tasks (default: 1).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithTasksMax(int tasksMax)
    {
        if (tasksMax < 1)
            throw new ArgumentException("Tasks max must be at least 1", nameof(tasksMax));
        _tasksMax = tasksMax;
        return this;
    }
    #region Database-Specific Configuration Methods
    /// <summary>
    /// Configures PostgreSQL logical replication settings.
    /// Required for PostgreSQL connectors.
    /// </summary>
    /// <param name="publicationName">The name of the PostgreSQL publication to use for CDC.</param>
    /// <param name="slotName">The name of the PostgreSQL replication slot.</param>
    /// <param name="pluginName">The logical decoding plugin (default: "pgoutput"). Options: pgoutput, decoderbufs.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if database type is not Postgres.</exception>
    public DebeziumSourceConnectorBuilder WithPostgresReplication(
        string publicationName,
        string slotName,
        string pluginName = "pgoutput")
    {
        if (_databaseType.HasValue && _databaseType != DatabaseType.Postgres)
            throw new InvalidOperationException("WithPostgresReplication() can only be used with DatabaseType.Postgres");
        _postgresPublicationName = publicationName ?? throw new ArgumentNullException(nameof(publicationName));
        _postgresSlotName = slotName ?? throw new ArgumentNullException(nameof(slotName));
        _postgresPluginName = pluginName ?? throw new ArgumentNullException(nameof(pluginName));
        return this;
    }
    /// <summary>
    /// Configures DB2 ASN (Apply/Capture) settings for change data capture.
    /// Required for DB2 connectors.
    /// </summary>
    /// <param name="asnProgram">The ASN capture program name (e.g., "ASN").</param>
    /// <param name="asnLib">The ASN library name (e.g., "ASNCAP").</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if database type is not Db2.</exception>
    public DebeziumSourceConnectorBuilder WithDb2Asn(string asnProgram, string asnLib)
    {
        if (_databaseType.HasValue && _databaseType != DatabaseType.Db2)
            throw new InvalidOperationException("WithDb2Asn() can only be used with DatabaseType.Db2");
        _db2AsnProgram = asnProgram ?? throw new ArgumentNullException(nameof(asnProgram));
        _db2AsnLib = asnLib ?? throw new ArgumentNullException(nameof(asnLib));
        return this;
    }
    /// <summary>
    /// Configures the MySQL server ID for binlog replication.
    /// Required for MySQL connectors to uniquely identify this connector in the replication topology.
    /// </summary>
    /// <param name="serverId">A unique server ID (must be unique across all MySQL replicas).</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if database type is not MySQL.</exception>
    /// <exception cref="ArgumentException">Thrown if serverId is less than 1.</exception>
    public DebeziumSourceConnectorBuilder WithMySqlServerId(int serverId)
    {
        if (_databaseType.HasValue && _databaseType != DatabaseType.MySQL)
            throw new InvalidOperationException("WithMySqlServerId() can only be used with DatabaseType.MySQL");
        if (serverId < 1)
            throw new ArgumentException("Server ID must be at least 1", nameof(serverId));
        _mysqlServerId = serverId;
        return this;
    }
    /// <summary>
    /// Configures the MongoDB connection string for change streams.
    /// This overrides the standard database connection parameters for MongoDB.
    /// </summary>
    /// <param name="connectionString">The MongoDB connection string (e.g., "mongodb://user:pass@host:port/database").</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if database type is not MongoDB.</exception>
    public DebeziumSourceConnectorBuilder WithMongoDbConnectionString(string connectionString)
    {
        if (_databaseType.HasValue && _databaseType != DatabaseType.MongoDB)
            throw new InvalidOperationException("WithMongoDbConnectionString() can only be used with DatabaseType.MongoDB");
        _mongoDbConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        return this;
    }
    #endregion
    #region Transform Configuration Methods
    /// <summary>
    /// Enables the ExtractNewRecordState SMT (Single Message Transform) to unwrap Debezium
    /// envelope format into a flattened record structure.
    /// </summary>
    /// <param name="enabled">Whether to enable the unwrap transform (default: true).</param>
    /// <param name="deleteMode">How to handle delete events (default: Rewrite).</param>
    /// <param name="addFields">Whether to add metadata fields like __op, __source_ts_ms (default: true).</param>
    /// <param name="dropTombstones">Whether to drop tombstone records (default: true).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithUnwrapTransform(
        bool enabled = true,
        DeleteHandlingMode deleteMode = DeleteHandlingMode.Rewrite,
        bool addFields = true,
        bool dropTombstones = true)
    {
        _unwrapTransformEnabled = enabled;
        _deleteHandlingMode = deleteMode;
        _unwrapAddFields = addFields;
        _dropTombstones = dropTombstones;
        return this;
    }
    /// <summary>
    /// Configures the RegexRouter SMT for topic routing and renaming.
    /// Useful for mapping Debezium's default topic naming to custom patterns.
    /// </summary>
    /// <param name="regex">Regular expression pattern to match topic names.</param>
    /// <param name="replacement">Replacement pattern (can use capture groups like $1, $2).</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// // Route topics from "m3-cdc.public.tablename" to "bronze.m3.tablename"
    /// .WithRouteTransform(@"m3-cdc\.public\.(.*)", "bronze.m3.$1")
    /// </example>
    public DebeziumSourceConnectorBuilder WithRouteTransform(string regex, string replacement)
    {
        _routeTransformRegex = regex ?? throw new ArgumentNullException(nameof(regex));
        _routeTransformReplacement = replacement ?? throw new ArgumentNullException(nameof(replacement));
        return this;
    }
    #endregion
    #region Schema Registry and Converter Configuration
    /// <summary>
    /// Configures the Schema Registry connection.
    /// </summary>
    /// <param name="url">Schema Registry URL.</param>
    /// <param name="auth">Authentication credentials in "username:password" format (can use ${env:VAR} syntax).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithSchemaRegistry(string url, string auth)
    {
        _schemaRegistryUrl = url ?? throw new ArgumentNullException(nameof(url));
        _schemaRegistryAuth = auth ?? throw new ArgumentNullException(nameof(auth));
        return this;
    }
    /// <summary>
    /// Configures the connector to use Avro serialization with Schema Registry.
    /// This is the default serialization format.
    /// </summary>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithAvroConverter()
    {
        _useAvroConverter = true;
        return this;
    }
    /// <summary>
    /// Configures the connector to use JSON serialization instead of Avro.
    /// Note: JSON does not require Schema Registry but lacks schema evolution capabilities.
    /// </summary>
    /// <param name="schemasEnable">Whether to include schema information in JSON messages (default: false).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithJsonConverter(bool schemasEnable = false)
    {
        _useAvroConverter = false;
        _jsonSchemasEnable = schemasEnable;
        return this;
    }
    #endregion
    #region Error Handling Configuration
    /// <summary>
    /// Configures error tolerance for the connector.
    /// When enabled (tolerateAll = true), errors are logged but don't stop the connector.
    /// </summary>
    /// <param name="tolerateAll">If true, tolerates all errors and logs them. If false, fails fast on errors.</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithErrorTolerance(bool tolerateAll = true)
    {
        _errorToleranceAll = tolerateAll;
        return this;
    }
    /// <summary>
    /// Configures a Dead Letter Queue (DLQ) topic for failed records.
    /// Records that cannot be processed are sent to this topic for later analysis.
    /// </summary>
    /// <param name="topicName">The DLQ topic name. If not specified, auto-derives from connector naming.</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithDeadLetterQueue(string topicName)
    {
        _dlqTopicName = topicName ?? throw new ArgumentNullException(nameof(topicName));
        return this;
    }
    #endregion
    #region Performance Tuning Configuration
    /// <summary>
    /// Configures performance-related settings for the connector.
    /// </summary>
    /// <param name="maxBatchSize">Maximum number of records in a batch (default: 2048).</param>
    /// <param name="maxQueueSize">Maximum size of the blocking queue for buffering records (default: 8192).</param>
    /// <param name="pollIntervalMs">Interval in milliseconds between polls for new CDC events (default: 1000).</param>
    /// <returns>The builder for method chaining.</returns>
    public DebeziumSourceConnectorBuilder WithPerformanceTuning(
        int maxBatchSize = 2048,
        int maxQueueSize = 8192,
        int pollIntervalMs = 1000)
    {
        if (maxBatchSize < 1)
            throw new ArgumentException("Max batch size must be at least 1", nameof(maxBatchSize));
        if (maxQueueSize < 1)
            throw new ArgumentException("Max queue size must be at least 1", nameof(maxQueueSize));
        if (pollIntervalMs < 0)
            throw new ArgumentException("Poll interval must be non-negative", nameof(pollIntervalMs));
        _maxBatchSize = maxBatchSize;
        _maxQueueSize = maxQueueSize;
        _pollIntervalMs = pollIntervalMs;
        return this;
    }
    #endregion
    private static string ComputeConfigHash(Dictionary<string, object> config)
    {
        var json = JsonSerializer.Serialize(config);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
    /// <summary>
    /// Builds the Debezium source connector as a Pulumi ComponentResource.
    /// </summary>
    /// <returns>A ComponentResource representing the KafkaConnector custom resource.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
    public ComponentResource Build()
    {
        // Validate required configuration
        ValidateConfiguration();
        // Derive names from DD130 naming convention if configured
        var (resolvedConnectorName, resolvedTopicPrefix, resolvedDlqTopic) = ResolveNaming();
        // Build the configuration dictionary
        var config = BuildConfiguration(resolvedTopicPrefix, resolvedDlqTopic);
        // Create the connector resource
        return CreateConnectorResource(resolvedConnectorName, config);
    }
    private void ValidateConfiguration()
    {
        if (!_databaseType.HasValue)
            throw new InvalidOperationException("Database type must be set using WithDatabaseType()");
        // MongoDB uses connection string instead of individual connection parameters
        if (_databaseType == DatabaseType.MongoDB)
        {
            if (string.IsNullOrEmpty(_mongoDbConnectionString))
                throw new InvalidOperationException(
                    "MongoDB connection string must be set using WithMongoDbConnectionString()");
        }
        else
        {
            if (string.IsNullOrEmpty(_databaseHostname) || string.IsNullOrEmpty(_databasePort) ||
                string.IsNullOrEmpty(_databaseUser) || string.IsNullOrEmpty(_databasePassword) ||
                string.IsNullOrEmpty(_databaseName))
                throw new InvalidOperationException(
                    "Database connection must be configured using WithDatabaseConnection()");
        }
        // Validate PostgreSQL-specific requirements
        if (_databaseType == DatabaseType.Postgres && string.IsNullOrEmpty(_postgresSlotName))
            throw new InvalidOperationException(
                "PostgreSQL connector requires replication configuration via WithPostgresReplication()");
        // Must have either DD130 naming or explicit topic prefix
        if (!_layer.HasValue && string.IsNullOrEmpty(_topicPrefix))
            throw new InvalidOperationException(
                "Either WithNaming() or WithTopicPrefix() must be called to configure topic naming");
    }
    private (string connectorName, string topicPrefix, string? dlqTopic) ResolveNaming()
    {
        string connectorName;
        string topicPrefix;
        string? dlqTopic = _dlqTopicName;
        if (_layer.HasValue && !string.IsNullOrEmpty(_domain) && !string.IsNullOrEmpty(_dataset))
        {
            // Use DD130 naming convention
            var components = new NamingConventionHelper.TopicComponents(
                _environment,
                _layer.Value,
                _domain,
                _subdomain,
                _dataset,
                _processingStage);
            // Derive topic prefix from DD130 (layer.domain or layer.domain.subdomain)
            topicPrefix = _topicPrefix ?? BuildTopicPrefix(components);
            connectorName = _connectorName ?? NamingConventionHelper.ToConnectorName(components);
            // Auto-derive DLQ topic if error tolerance is enabled but no DLQ was specified
            if (_errorToleranceAll && string.IsNullOrEmpty(dlqTopic))
            {
                var baseTopicName = NamingConventionHelper.ToTopicName(components);
                dlqTopic = NamingConventionHelper.ToDlqTopic(baseTopicName);
            }
        }
        else
        {
            // Use explicit naming
            topicPrefix = _topicPrefix!;
            connectorName = _connectorName ?? $"{topicPrefix.Replace(".", "-")}-source";
        }
        return (connectorName, topicPrefix, dlqTopic);
    }
    private static string BuildTopicPrefix(NamingConventionHelper.TopicComponents components)
    {
        // For CDC, topic prefix is typically: layer.domain (Debezium appends schema.table)
        var parts = new List<string>
        {
            components.Layer.ToString().ToLowerInvariant(),
            components.Domain
        };
        if (!string.IsNullOrEmpty(components.Subdomain))
            parts.Add(components.Subdomain);
        return string.Join(".", parts);
    }
    private Dictionary<string, object> BuildConfiguration(string topicPrefix, string? dlqTopic)
    {
        var config = new Dictionary<string, object>
        {
            // Connector identity
            ["topic.prefix"] = topicPrefix,
            // Snapshot configuration
            ["snapshot.mode"] = GetSnapshotModeString()
        };
        // Database connection (varies by database type)
        AddDatabaseConnectionConfig(config);
        // Database-specific configuration
        AddDatabaseSpecificConfig(config);
        // Table selection
        if (!string.IsNullOrEmpty(_tableIncludeList))
            config["table.include.list"] = _tableIncludeList;
        if (!string.IsNullOrEmpty(_tableExcludeList))
            config["table.exclude.list"] = _tableExcludeList;
        // Transforms
        AddTransformConfig(config);
        // Converters
        AddConverterConfig(config);
        // Error handling
        AddErrorHandlingConfig(config, dlqTopic);
        // Performance tuning
        config["max.batch.size"] = _maxBatchSize;
        config["max.queue.size"] = _maxQueueSize;
        config["poll.interval.ms"] = _pollIntervalMs;
        return config;
    }
    private void AddDatabaseConnectionConfig(Dictionary<string, object> config)
    {
        if (_databaseType == DatabaseType.MongoDB)
        {
            config["mongodb.connection.string"] = _mongoDbConnectionString!;
        }
        else
        {
            config["database.hostname"] = _databaseHostname!;
            config["database.port"] = _databasePort!;
            config["database.user"] = _databaseUser!;
            config["database.password"] = _databasePassword!;
            config["database.dbname"] = _databaseName!;
        }
    }
    private void AddDatabaseSpecificConfig(Dictionary<string, object> config)
    {
        switch (_databaseType)
        {
            case DatabaseType.Postgres:
                if (!string.IsNullOrEmpty(_postgresPluginName))
                    config["plugin.name"] = _postgresPluginName;
                if (!string.IsNullOrEmpty(_postgresPublicationName))
                    config["publication.name"] = _postgresPublicationName;
                if (!string.IsNullOrEmpty(_postgresSlotName))
                    config["slot.name"] = _postgresSlotName;
                break;
            case DatabaseType.Db2:
                if (!string.IsNullOrEmpty(_db2AsnProgram))
                    config["asn.capture.program"] = _db2AsnProgram;
                if (!string.IsNullOrEmpty(_db2AsnLib))
                    config["asn.capture.library"] = _db2AsnLib;
                break;
            case DatabaseType.MySQL:
                if (_mysqlServerId.HasValue)
                    config["database.server.id"] = _mysqlServerId.Value;
                break;
            case DatabaseType.SqlServer:
                // SqlServer uses the same connection parameters, no additional config needed
                break;
            case DatabaseType.Oracle:
                // Oracle uses the same connection parameters, no additional config needed
                break;
            case DatabaseType.MongoDB:
                // MongoDB connection is handled in AddDatabaseConnectionConfig
                break;
        }
    }
    private void AddTransformConfig(Dictionary<string, object> config)
    {
        var transforms = new List<string>();
        // Unwrap transform (ExtractNewRecordState)
        if (_unwrapTransformEnabled)
        {
            transforms.Add("unwrap");
            config["transforms.unwrap.type"] = "io.debezium.transforms.ExtractNewRecordState";
            config["transforms.unwrap.delete.handling.mode"] = GetDeleteHandlingModeString();
            config["transforms.unwrap.drop.tombstones"] = _dropTombstones;
            if (_unwrapAddFields)
            {
                config["transforms.unwrap.add.fields"] = "op,source.ts_ms";
                config["transforms.unwrap.add.headers"] = "op,source.ts_ms";
            }
        }
        // Route transform (RegexRouter)
        if (!string.IsNullOrEmpty(_routeTransformRegex) && !string.IsNullOrEmpty(_routeTransformReplacement))
        {
            transforms.Add("route");
            config["transforms.route.type"] = "org.apache.kafka.connect.transforms.RegexRouter";
            config["transforms.route.regex"] = _routeTransformRegex;
            config["transforms.route.replacement"] = _routeTransformReplacement;
        }
        if (transforms.Count > 0)
            config["transforms"] = string.Join(",", transforms);
    }
    private void AddConverterConfig(Dictionary<string, object> config)
    {
        if (_useAvroConverter)
        {
            // Avro with Schema Registry
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
        else
        {
            // JSON converter
            config["key.converter"] = "org.apache.kafka.connect.json.JsonConverter";
            config["key.converter.schemas.enable"] = _jsonSchemasEnable;
            config["value.converter"] = "org.apache.kafka.connect.json.JsonConverter";
            config["value.converter.schemas.enable"] = _jsonSchemasEnable;
        }
    }
    private void AddErrorHandlingConfig(Dictionary<string, object> config, string? dlqTopic)
    {
        if (_errorToleranceAll)
        {
            config["errors.tolerance"] = "all";
            if (!string.IsNullOrEmpty(dlqTopic))
            {
                config["errors.deadletterqueue.topic.name"] = dlqTopic;
                config["errors.deadletterqueue.context.headers.enable"] = true;
            }
        }
        else
        {
            config["errors.tolerance"] = "none";
        }
    }
    private ComponentResource CreateConnectorResource(string connectorName, Dictionary<string, object> config)
    {
        // Create a container component resource
        var component = new DebeziumConnectorComponent(
            connectorName,
            _manifestsRoot,
            _clusterName,
            _tasksMax,
            GetConnectorClass(),
            config);
        return component;
    }
}
/// <summary>
/// Internal component resource that holds the Debezium connector Kubernetes resources.
/// </summary>
internal class DebeziumConnectorComponent : ComponentResource
{
    public DebeziumConnectorComponent(
        string name,
        string manifestsRoot,
        string clusterName,
        int tasksMax,
        string connectorClass,
        Dictionary<string, object> config)
        : base("debezium-source-connector", name)
    {
        var provider = new Kubernetes.Provider($"yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = this
        });
        var connector = new Kubernetes.ApiExtensions.CustomResource(name,
            new KafkaConnectorArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = name,
                    Namespace = "kafka-connect",
                    Labels = new Dictionary<string, string>
                    {
                        { "strimzi.io/cluster", clusterName }
                    },
                    Annotations = new Dictionary<string, string>
                    {
                        { "config-hash", ComputeConfigHash(config) }
                    }
                },
                Spec = new Dictionary<string, object>
                {
                    ["class"] = connectorClass,
                    ["tasksMax"] = tasksMax,
                    ["config"] = config
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });
    }
    private static string ComputeConfigHash(Dictionary<string, object> config)
    {
        var json = JsonSerializer.Serialize(config);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
    private class KafkaConnectorArgs : Kubernetes.ApiExtensions.CustomResourceArgs
    {
        public KafkaConnectorArgs() : base("kafka.strimzi.io/v1beta2", "KafkaConnector")
        {
        }
        [Input("spec")] public Dictionary<string, object>? Spec { get; set; }
    }
}