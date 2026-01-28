# Implementation Report: Debezium Connector Builder Pattern

## Summary

Successfully refactored the `PostgresDebeziumConnector` into a generic `DebeziumSourceConnectorBuilder` that supports multiple database types using the fluent builder pattern. The new builder follows the established patterns in the codebase (similar to `IcebergSinkConnectorBuilder`) and integrates with DD130 naming conventions.

---

## What Was Implemented

### New Files Created

| File | Description | Lines |
|------|-------------|-------|
| `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs` | Generic fluent builder for Debezium source connectors | 932 |

### Files Modified

| File | Change |
|------|--------|
| `gitops/applications/Program.cs` | Replaced `PostgresDebeziumConnector` with `DebeziumSourceConnectorBuilder` usage |

### Files Deleted

| File | Reason |
|------|--------|
| `gitops/applications/kafkaconnect/PostgresDebeziumConnector.cs` | Replaced by the generic builder |

---

## Implementation Details

### Core Enums

Three enums were added to support type-safe configuration:

```csharp
public enum DatabaseType { Postgres, Db2, MySQL, SqlServer, Oracle, MongoDB }
public enum SnapshotMode { Initial, Always, Never, SchemaOnly, WhenNeeded }
public enum DeleteHandlingMode { None, Rewrite, Drop }
```

### Builder API

The builder implements a comprehensive fluent API:

**Core Configuration:**
- `WithDatabaseType()` - Required: Sets the target database type
- `WithDatabaseConnection()` - Database credentials (hostname, port, user, password, database)

**Naming Configuration:**
- `WithNaming()` - DD130 naming convention (auto-derives topic prefix, connector name, DLQ)
- `WithTopicPrefix()` - Explicit topic prefix
- `WithConnectorName()` - Override auto-generated connector name

**Table Selection:**
- `WithTableIncludeList()` - Tables to capture CDC from
- `WithTableExcludeList()` - Tables to exclude

**Database-Specific Methods:**
- `WithPostgresReplication()` - PostgreSQL publication, slot, plugin
- `WithDb2Asn()` - DB2 ASN capture program and library
- `WithMySqlServerId()` - MySQL binlog server ID
- `WithMongoDbConnectionString()` - MongoDB connection string

**Transforms:**
- `WithUnwrapTransform()` - ExtractNewRecordState SMT configuration
- `WithRouteTransform()` - RegexRouter SMT for topic routing

**Schema & Serialization:**
- `WithSchemaRegistry()` - Schema Registry URL and auth
- `WithAvroConverter()` - Use Avro serialization (default)
- `WithJsonConverter()` - Use JSON serialization

**Error Handling:**
- `WithErrorTolerance()` - Error handling mode
- `WithDeadLetterQueue()` - DLQ topic configuration

**Performance:**
- `WithPerformanceTuning()` - Batch size, queue size, poll interval
- `WithTasksMax()` - Connector parallelism
- `WithClusterName()` - Strimzi cluster label

### Connector Class Mapping

The builder maps `DatabaseType` to Debezium connector class names:

| Database Type | Connector Class |
|---------------|-----------------|
| Postgres | `io.debezium.connector.postgresql.PostgresConnector` |
| Db2 | `io.debezium.connector.db2.Db2Connector` |
| MySQL | `io.debezium.connector.mysql.MySqlConnector` |
| SqlServer | `io.debezium.connector.sqlserver.SqlServerConnector` |
| Oracle | `io.debezium.connector.oracle.OracleConnector` |
| MongoDB | `io.debezium.connector.mongodb.MongoDbConnector` |

---

## Testing & Verification

### Build Verification

The solution compiles without errors:

```bash
cd gitops/applications
dotnet build
# Build succeeded.
```

### Manifest Generation

Running `dotnet run` generates the same YAML manifest as the original `PostgresDebeziumConnector`:

```bash
dotnet run
# Generates: gitops/manifests/kafka-connect/postgres-debezium-source.yaml
```

### Migration Verification

The migrated `Program.cs` usage matches the original PostgreSQL CDC configuration exactly:

```csharp
var postgresDebeziumSource = new DebeziumSourceConnectorBuilder("../manifests")
    .WithDatabaseType(DatabaseType.Postgres)
    .WithDatabaseConnection(
        hostname: "${env:POSTGRES_HOST}",
        port: "${env:POSTGRES_PORT}",
        user: "${env:POSTGRES_USER}",
        password: "${env:POSTGRES_PASSWORD}",
        database: "${env:POSTGRES_DB}")
    .WithConnectorName("postgres-debezium-source")
    .WithTopicPrefix("m3-cdc")
    .WithPostgresReplication(
        publicationName: "dbz_m3_publication",
        slotName: "m3_debezium_slot",
        pluginName: "pgoutput")
    .WithTableIncludeList("public.CSYTAB", "public.CIDMAS", "public.CIDVEN")
    .WithSnapshotMode(SnapshotMode.Always)
    .WithUnwrapTransform(
        enabled: true,
        deleteMode: DeleteHandlingMode.Rewrite,
        addFields: true,
        dropTombstones: true)
    .WithRouteTransform(
        regex: "m3-cdc.public.(.*)",
        replacement: "bronze.m3.$1")
    .WithAvroConverter()
    .WithErrorTolerance(tolerateAll: true)
    .WithDeadLetterQueue("m3-debezium-errors")
    .WithPerformanceTuning(
        maxBatchSize: 2048,
        maxQueueSize: 8192,
        pollIntervalMs: 1000)
    .Build();
```

---

## Commit History

The implementation was completed in 6 incremental commits:

1. **f71cd9a** - Create DebeziumSourceConnectorBuilder Core Structure
2. **e8d7ea7** - Add DD130 Naming and Common Configuration Methods
3. **41e2f8d** - Add Database-Specific Configuration Methods
4. **e2024fb** - Add Transform, Schema, and Error Handling Methods
5. **6c773ec** - Implement Build Method and Connector Class Mapping
6. **f1bd083** - Migrate Program.cs and Verify Output

---

## Challenges & Decisions

### 1. MongoDB Connection Handling

MongoDB uses a connection string instead of individual connection parameters. The builder validates this at build time and uses the appropriate configuration format:

```csharp
if (_databaseType == DatabaseType.MongoDB)
    config["mongodb.connection.string"] = _mongoDbConnectionString!;
else
    // Use individual hostname, port, user, password, database
```

### 2. Database-Specific Method Validation

Database-specific methods (e.g., `WithPostgresReplication()`) validate that they're only used with the correct database type to prevent misconfiguration:

```csharp
public DebeziumSourceConnectorBuilder WithPostgresReplication(...)
{
    if (_databaseType.HasValue && _databaseType != DatabaseType.Postgres)
        throw new InvalidOperationException(
            "WithPostgresReplication() can only be used with DatabaseType.Postgres");
    // ...
}
```

### 3. Config Hash for Change Detection

The builder computes a SHA256 hash of the connector configuration and stores it in a Kubernetes annotation. This enables change detection for GitOps workflows:

```csharp
Annotations = new Dictionary<string, string>
{
    { "config-hash", ComputeConfigHash(config) }
}
```

---

## Future Extensions

The builder is designed to easily support future database types:

1. Add new value to `DatabaseType` enum
2. Add case to `GetConnectorClass()` switch
3. Optionally add database-specific configuration method (e.g., `WithOracleLogMiner()`)
4. Add database-specific configuration in `AddDatabaseSpecificConfig()`

---

## Files Reference

- **Implementation**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`
- **Usage Example**: `gitops/applications/Program.cs` (lines 20-53)
- **Specification**: `.zenflow/tasks/debezium-connector-8dda/spec.md`
