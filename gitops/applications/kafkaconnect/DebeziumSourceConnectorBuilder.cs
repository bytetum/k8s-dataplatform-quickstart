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

    private static string ComputeConfigHash(Dictionary<string, object> config)
    {
        var json = JsonSerializer.Serialize(config);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
