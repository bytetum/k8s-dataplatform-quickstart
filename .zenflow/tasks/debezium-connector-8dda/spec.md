# Technical Specification: Debezium Connector Builder Pattern

## Task Summary
Refactor the existing `PostgresDebeziumConnector` into a generic `DebeziumSourceConnectorBuilder` that supports multiple database types (PostgreSQL, DB2, MySQL, etc.) using the fluent builder pattern already established in the codebase.

## Difficulty Assessment: **Medium**
- The codebase already has a well-established builder pattern (`IcebergSinkConnectorBuilder`) to follow
- The existing `PostgresDebeziumConnector` provides the reference implementation
- Moderate complexity due to database-specific configurations that need abstraction
- No architectural changes needed - follows existing patterns

---

## Technical Context

### Language & Framework
- **Language**: C# (.NET)
- **Framework**: Pulumi (Infrastructure as Code)
- **Platform**: Kubernetes (Strimzi Kafka Operator)

### Dependencies
- `Pulumi.Kubernetes` - Kubernetes resource management
- `Pulumi.Crds.KafkaConnect` - Custom resource definitions for KafkaConnector
- Strimzi Kafka Operator - Manages KafkaConnector CRDs in Kubernetes

### Existing Patterns to Follow
The codebase uses a consistent fluent builder pattern:
- `IcebergSinkConnectorBuilder` (507 lines) - Comprehensive example with DD130 naming integration
- `KafkaConnectClusterBuilder` - Simpler builder for Kafka Connect cluster configuration
- `FlinkDeploymentBuilder` - Similar pattern for Flink jobs

---

## Implementation Approach

### Design Principles
1. **Follow existing patterns**: Mirror `IcebergSinkConnectorBuilder` structure and conventions
2. **Database abstraction**: Use an enum for database types with type-specific configuration
3. **DD130 naming integration**: Leverage `NamingConventionHelper` for consistent naming
4. **Sensible defaults**: Provide reasonable defaults while allowing full customization
5. **Type safety**: Use strongly-typed configuration where possible

### Database Type Support
```csharp
public enum DatabaseType
{
    Postgres,
    Db2,
    MySQL,
    SqlServer,
    Oracle,
    MongoDB
}
```

### Core Builder Methods (Common to All Databases)

| Method | Description |
|--------|-------------|
| `WithDatabaseType(DatabaseType)` | Set the database type (required) |
| `WithNaming(layer, domain, dataset, ...)` | DD130 naming convention integration |
| `WithTopicPrefix(string)` | Set Debezium topic prefix |
| `WithTableIncludeList(params string[])` | Tables to capture CDC from |
| `WithSnapshotMode(SnapshotMode)` | Control snapshot behavior |
| `WithSchemaRegistry(url, auth)` | Schema registry configuration |
| `WithTransforms(...)` | Configure SMT chain (unwrap, route) |
| `WithErrorHandling(...)` | DLQ and error tolerance settings |
| `WithPerformanceTuning(...)` | Batch size, queue size, poll interval |

### Database-Specific Methods

**PostgreSQL:**
- `WithPublicationName(string)` - PostgreSQL publication
- `WithSlotName(string)` - PostgreSQL replication slot
- `WithPluginName(string)` - pgoutput/decoderbufs

**DB2:**
- `WithAsnProgram(string)` - ASN capture program
- `WithAsnLib(string)` - ASN library

**MySQL:**
- `WithServerId(int)` - MySQL server ID for binlog

**MongoDB:**
- `WithConnectionString(string)` - MongoDB connection string

---

## Source Code Structure Changes

### Files to Create

| File | Description |
|------|-------------|
| `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs` | Main builder class (~300-400 lines) |

### Files to Modify

| File | Change |
|------|--------|
| `gitops/applications/Program.cs` | Replace `PostgresDebeziumConnector` with `DebeziumSourceConnectorBuilder` usage |
| `gitops/applications/kafkaconnect/PostgresDebeziumConnector.cs` | Mark as obsolete or delete after migration |

### Files Unchanged
- `pulumi/kafkaconnect/KafkaConnector.cs` - CRD definition already supports generic configs
- `gitops/applications/kafkaconnect/NamingConventionHelper.cs` - Already generic enough
- `gitops/applications/kafkaconnect/IcebergSinkConnectorBuilder.cs` - Reference only

---

## Data Model / API Changes

### New Enums

```csharp
public enum DatabaseType { Postgres, Db2, MySQL, SqlServer, Oracle, MongoDB }
public enum SnapshotMode { Initial, Always, Never, SchemaOnly, WhenNeeded }
public enum DeleteHandlingMode { None, Rewrite, Drop }
```

### Builder Interface

```csharp
public class DebeziumSourceConnectorBuilder
{
    // Constructor
    public DebeziumSourceConnectorBuilder(string manifestsRoot);

    // Required configuration
    public DebeziumSourceConnectorBuilder WithDatabaseType(DatabaseType type);
    public DebeziumSourceConnectorBuilder WithDatabaseConnection(
        string host, string port, string user, string password, string database);

    // DD130 naming (auto-derives topic prefix, connector name, DLQ)
    public DebeziumSourceConnectorBuilder WithNaming(
        DataLayer layer, string domain, string dataset,
        string? subdomain = null, string? processingStage = null);

    // Alternative: explicit naming
    public DebeziumSourceConnectorBuilder WithTopicPrefix(string prefix);
    public DebeziumSourceConnectorBuilder WithConnectorName(string name);

    // Table selection
    public DebeziumSourceConnectorBuilder WithTableIncludeList(params string[] tables);
    public DebeziumSourceConnectorBuilder WithTableExcludeList(params string[] tables);

    // Snapshot configuration
    public DebeziumSourceConnectorBuilder WithSnapshotMode(SnapshotMode mode);

    // PostgreSQL-specific
    public DebeziumSourceConnectorBuilder WithPostgresReplication(
        string publicationName, string slotName, string pluginName = "pgoutput");

    // DB2-specific
    public DebeziumSourceConnectorBuilder WithDb2Asn(string asnProgram, string asnLib);

    // MySQL-specific
    public DebeziumSourceConnectorBuilder WithMySqlServerId(int serverId);

    // MongoDB-specific
    public DebeziumSourceConnectorBuilder WithMongoDbConnectionString(string connectionString);

    // Transforms
    public DebeziumSourceConnectorBuilder WithUnwrapTransform(
        bool enabled = true,
        DeleteHandlingMode deleteMode = DeleteHandlingMode.Rewrite,
        bool addFields = true);
    public DebeziumSourceConnectorBuilder WithRouteTransform(string regex, string replacement);

    // Schema handling
    public DebeziumSourceConnectorBuilder WithSchemaRegistry(string url, string auth);
    public DebeziumSourceConnectorBuilder WithAvroConverter();
    public DebeziumSourceConnectorBuilder WithJsonConverter(bool schemasEnable = false);

    // Error handling
    public DebeziumSourceConnectorBuilder WithErrorTolerance(ErrorTolerance tolerance);
    public DebeziumSourceConnectorBuilder WithDeadLetterQueue(string topicName);

    // Performance
    public DebeziumSourceConnectorBuilder WithPerformanceTuning(
        int maxBatchSize = 2048, int maxQueueSize = 8192, int pollIntervalMs = 1000);

    // Cluster configuration
    public DebeziumSourceConnectorBuilder WithClusterName(string clusterName);
    public DebeziumSourceConnectorBuilder WithTasksMax(int tasksMax);

    // Build
    public ComponentResource Build();
}
```

---

## Connector Class Mapping

| Database Type | Debezium Connector Class |
|---------------|--------------------------|
| Postgres | `io.debezium.connector.postgresql.PostgresConnector` |
| Db2 | `io.debezium.connector.db2.Db2Connector` |
| MySQL | `io.debezium.connector.mysql.MySqlConnector` |
| SqlServer | `io.debezium.connector.sqlserver.SqlServerConnector` |
| Oracle | `io.debezium.connector.oracle.OracleConnector` |
| MongoDB | `io.debezium.connector.mongodb.MongoDbConnector` |

---

## Usage Examples

### PostgreSQL CDC (Current Use Case)
```csharp
var postgresDebezium = new DebeziumSourceConnectorBuilder("../manifests")
    .WithDatabaseType(DatabaseType.Postgres)
    .WithNaming(DataLayer.Bronze, domain: "m3", dataset: "cdc")
    .WithDatabaseConnection(
        host: "${env:POSTGRES_HOST}",
        port: "${env:POSTGRES_PORT}",
        user: "${env:POSTGRES_USER}",
        password: "${env:POSTGRES_PASSWORD}",
        database: "${env:POSTGRES_DB}")
    .WithPostgresReplication(
        publicationName: "dbz_m3_publication",
        slotName: "m3_debezium_slot")
    .WithTableIncludeList("public.CSYTAB", "public.CIDMAS", "public.CIDVEN")
    .WithSnapshotMode(SnapshotMode.Always)
    .WithUnwrapTransform(deleteMode: DeleteHandlingMode.Rewrite)
    .WithRouteTransform(@"m3-cdc\.public\.(.*)", "bronze.m3.$1")
    .Build();
```

### DB2 CDC (Future Use Case)
```csharp
var db2Debezium = new DebeziumSourceConnectorBuilder("../manifests")
    .WithDatabaseType(DatabaseType.Db2)
    .WithNaming(DataLayer.Bronze, domain: "erp", dataset: "orders")
    .WithDatabaseConnection(
        host: "${env:DB2_HOST}",
        port: "${env:DB2_PORT}",
        user: "${env:DB2_USER}",
        password: "${env:DB2_PASSWORD}",
        database: "${env:DB2_DB}")
    .WithDb2Asn(asnProgram: "ASN", asnLib: "ASNCAP")
    .WithTableIncludeList("DB2ADMIN.ORDERS", "DB2ADMIN.ORDER_ITEMS")
    .Build();
```

---

## Verification Approach

### Unit Testing
- Builder correctly generates configuration dictionaries for each database type
- DD130 naming conventions are applied correctly
- Required parameters are validated

### Integration Testing
- Generated YAML manifests match expected Strimzi KafkaConnector format
- Configuration hash is computed correctly for change detection
- Kubernetes metadata (labels, annotations) are correct

### Manual Verification
1. Run `dotnet run` in `gitops/applications/` to generate manifests
2. Compare generated YAML against existing `postgres-debezium-source` manifest
3. Validate YAML with `kubectl apply --dry-run=client -f <manifest>`

### Lint/Build Commands
```bash
cd gitops/applications
dotnet build
dotnet run  # Generates manifests to ../manifests/
```

---

## Migration Notes

### Backward Compatibility
The new builder should produce identical YAML output for the current PostgreSQL configuration, ensuring:
- Same connector name: `postgres-debezium-source`
- Same configuration keys and values
- Same labels and annotations

### Deprecation Path
1. Create `DebeziumSourceConnectorBuilder` alongside existing `PostgresDebeziumConnector`
2. Update `Program.cs` to use the new builder
3. Verify generated manifests are identical
4. Remove or mark `PostgresDebeziumConnector` as `[Obsolete]`

---

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Configuration differences break existing CDC | Generate manifests and diff against current output |
| Database-specific options missed | Start with PostgreSQL parity, add others incrementally |
| DD130 naming conflicts with existing setup | Builder supports both DD130 and explicit naming |

---

## Out of Scope
- Schema validation job integration (already handled by `SchemaValidationJobBuilder`)
- Kafka Connect cluster configuration (handled by `KafkaConnectClusterBuilder`)
- Sink connectors (handled by `IcebergSinkConnectorBuilder`)
