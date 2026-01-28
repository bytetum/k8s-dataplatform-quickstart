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

    private static string ComputeConfigHash(Dictionary<string, object> config)
    {
        var json = JsonSerializer.Serialize(config);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
