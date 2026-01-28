# Technical Specification: Generic Debezium Connector Builder

## Task Summary
Refactor the `PostgresDebeziumConnector` into a generic builder pattern that supports multiple database types (PostgreSQL, DB2, MySQL, MongoDB, etc.) following established patterns in the codebase.

## Difficulty Assessment: **Medium**

**Rationale:**
- Clear pattern to follow (`IcebergSinkConnectorBuilder`)
- Well-defined configuration structure from existing implementation
- Moderate complexity in supporting multiple database types with different configurations
- Need to create missing `NamingConventionHelper` class (prerequisite)
- Architecture decision needed for database-specific vs. shared configuration

---

## Technical Context

### Language & Framework
- **Language:** C# (.NET 8.0)
- **IaC Framework:** Pulumi
- **Kubernetes:** Strimzi Kafka Connect (KafkaConnector CRD)
- **Package:** `applications.kafkaconnect`

### Dependencies
- `Pulumi` (core)
- `Pulumi.Kubernetes` (K8s resources)
- `Pulumi.Crds.KafkaConnect` (custom CRDs for KafkaConnector)
- `System.Security.Cryptography` (config hashing)
- `System.Text.Json` (serialization)

### Existing Builder Patterns
The codebase uses a **fluent builder pattern** consistently:
- `IcebergSinkConnectorBuilder` - Comprehensive example (500+ lines)
- `KafkaConnectClusterBuilder` - Cluster configuration
- `FlinkDeploymentBuilder` - Flink job deployments
- `SchemaValidationJobBuilder` - Schema validation jobs

**Common Pattern Characteristics:**
1. Private fields for configuration
2. `With*()` methods returning `this` for chaining
3. `Build()` method returning `ComponentResource`
4. SHA256 config hash for change detection
5. Pulumi Provider with `RenderYamlToDirectory`
6. DD130 naming convention integration

---

## Implementation Approach

### Strategy: **Database-Agnostic Builder with Database-Specific Presets**

Create a generic `DebeziumSourceConnectorBuilder` that:
1. Supports all Debezium connector classes via `WithConnectorClass()` or preset methods
2. Provides common configuration shared across all Debezium connectors
3. Offers database-specific preset methods (e.g., `ForPostgres()`, `ForDb2()`)
4. Follows the same fluent API pattern as `IcebergSinkConnectorBuilder`

### Supported Debezium Connectors (Initial Scope)
| Database | Connector Class | Priority |
|----------|----------------|----------|
| PostgreSQL | `io.debezium.connector.postgresql.PostgresConnector` | P1 |
| DB2 | `io.debezium.connector.db2.Db2Connector` | P1 |
| MySQL | `io.debezium.connector.mysql.MySqlConnector` | P2 |
| SQL Server | `io.debezium.connector.sqlserver.SqlServerConnector` | P2 |
| MongoDB | `io.debezium.connector.mongodb.MongoDbConnector` | P3 |

---

## Source Code Structure Changes

### Files to Create

#### 1. `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs` (NEW)
Main builder class with fluent API for all Debezium source connectors.

**Estimated size:** ~400-500 lines

**Structure:**
```csharp
public class DebeziumSourceConnectorBuilder
{
    // === Private fields for configuration ===
    private string _manifestsRoot;
    private string _connectorName;
    private string _connectorClass;
    private string _clusterName;
    private int _tasksMax;

    // Database connection
    private string _databaseHostname;
    private string _databasePort;
    private string _databaseUser;
    private string _databasePassword;
    private string _databaseName;

    // Debezium-specific
    private string _topicPrefix;
    private string _snapshotMode;
    private string _tableIncludeList;
    private string _pluginName;           // PostgreSQL-specific
    private string _publicationName;      // PostgreSQL-specific
    private string _slotName;             // PostgreSQL-specific

    // SMT configuration
    private bool _enableUnwrap;
    private bool _enableTopicRouting;
    private string _routeRegex;
    private string _routeReplacement;

    // Schema Registry
    private string _schemaRegistryUrl;
    private bool _useAvroConverter;

    // Error handling
    private bool _failFastMode;
    private string _dlqTopic;

    // DD130 Naming Convention
    private NamingConventionHelper.DataLayer? _layer;
    private string _domain;
    private string _dataset;
    // ... other naming fields

    // === Constructor ===
    public DebeziumSourceConnectorBuilder(string manifestsRoot);

    // === Database Preset Methods ===
    public DebeziumSourceConnectorBuilder ForPostgres();
    public DebeziumSourceConnectorBuilder ForDb2();
    public DebeziumSourceConnectorBuilder ForMySql();
    public DebeziumSourceConnectorBuilder ForSqlServer();
    public DebeziumSourceConnectorBuilder ForMongoDB();

    // === Common Configuration Methods ===
    public DebeziumSourceConnectorBuilder WithConnectorName(string name);
    public DebeziumSourceConnectorBuilder WithClusterName(string clusterName);
    public DebeziumSourceConnectorBuilder WithTasksMax(int tasksMax);

    // === Database Connection Methods ===
    public DebeziumSourceConnectorBuilder WithDatabaseConnection(
        string hostname, string port, string user, string password, string database);
    public DebeziumSourceConnectorBuilder WithDatabaseConnectionFromEnv();  // Default env vars

    // === Debezium Configuration Methods ===
    public DebeziumSourceConnectorBuilder WithTopicPrefix(string prefix);
    public DebeziumSourceConnectorBuilder WithSnapshotMode(string mode);
    public DebeziumSourceConnectorBuilder WithTableIncludeList(params string[] tables);
    public DebeziumSourceConnectorBuilder WithTableExcludeList(params string[] tables);

    // === PostgreSQL-specific Methods ===
    public DebeziumSourceConnectorBuilder WithReplicationSlot(string slotName);
    public DebeziumSourceConnectorBuilder WithPublication(string publicationName);
    public DebeziumSourceConnectorBuilder WithPlugin(string pluginName);  // pgoutput, decoderbufs

    // === SMT Configuration ===
    public DebeziumSourceConnectorBuilder WithUnwrap(
        bool dropTombstones = true,
        string deleteHandlingMode = "rewrite",
        string addFields = "op,source.ts_ms");
    public DebeziumSourceConnectorBuilder WithTopicRouting(string regex, string replacement);

    // === Schema Registry ===
    public DebeziumSourceConnectorBuilder WithSchemaRegistry(string url);
    public DebeziumSourceConnectorBuilder WithAvroConverter();
    public DebeziumSourceConnectorBuilder WithJsonConverter();

    // === Error Handling ===
    public DebeziumSourceConnectorBuilder WithFailFastMode();
    public DebeziumSourceConnectorBuilder WithDlqTopic(string topic);

    // === DD130 Naming ===
    public DebeziumSourceConnectorBuilder WithNaming(
        NamingConventionHelper.DataLayer layer,
        string domain,
        string dataset,
        string? subdomain = null,
        string? processingStage = null,
        string? environment = null);

    // === Build ===
    public ComponentResource Build();

    // === Private Helpers ===
    private static string ComputeConfigHash(Dictionary<string, object> config);
}
```

#### 2. `gitops/applications/NamingConventionHelper.cs` (NEW - Prerequisite)
Static helper class for DD130 naming convention (currently missing, causing build failures).

**Estimated size:** ~200 lines

**Structure:**
```csharp
public static class NamingConventionHelper
{
    public enum DataLayer { Bronze, Silver, Gold }

    public enum SchemaCompatibility { None, Backward, Forward, Full, BackwardTransitive, ForwardTransitive, FullTransitive }

    public record TopicComponents(
        string? Environment,
        DataLayer Layer,
        string Domain,
        string? Subdomain,
        string Dataset,
        string? ProcessingStage);

    // Parsing and generation methods
    public static TopicComponents ParseTopic(string topicName);
    public static string ToTopicName(TopicComponents components);
    public static string ToIcebergTable(TopicComponents components);
    public static string ToFlinkJobName(DataLayer layer, string domain, string dataset, ...);
    public static string ToDlqTopic(string sourceTopic);
    public static SchemaCompatibility GetDefaultCompatibility(DataLayer layer);
    public static string ToSchemaRegistryString(SchemaCompatibility compatibility);
}
```

### Files to Modify

#### 1. `gitops/applications/kafkaconnect/PostgresDebeziumConnector.cs`
- Mark as `[Obsolete("Use DebeziumSourceConnectorBuilder instead")]`
- Keep for backward compatibility initially
- Can be deleted after migration

#### 2. `gitops/applications/Program.cs`
- Replace `PostgresDebeziumConnector` instantiation with `DebeziumSourceConnectorBuilder`
- Example new usage pattern

---

## API / Interface Changes

### New Builder Usage Pattern

**Before (current hardcoded approach):**
```csharp
var postgreDebeziumConnector = new PostgresDebeziumConnector("../manifests");
```

**After (generic builder with presets):**
```csharp
// Using database preset + DD130 naming
var postgresDebeziumSource = new DebeziumSourceConnectorBuilder("../manifests")
    .ForPostgres()
    .WithNaming(NamingConventionHelper.DataLayer.Bronze, domain: "m3", dataset: "cidmas")
    .WithDatabaseConnectionFromEnv()
    .WithTableIncludeList("public.CSYTAB", "public.CIDMAS", "public.CIDVEN")
    .WithReplicationSlot("m3_debezium_slot")
    .WithPublication("dbz_m3_publication")
    .WithUnwrap()
    .WithTopicRouting("m3-cdc.public.(.*)", "bronze.m3.$1")
    .WithAvroConverter()
    .Build();

// Using explicit configuration (no presets)
var db2DebeziumSource = new DebeziumSourceConnectorBuilder("../manifests")
    .ForDb2()
    .WithConnectorName("db2-cdc-source")
    .WithDatabaseConnection(
        hostname: "${env:DB2_HOST}",
        port: "${env:DB2_PORT}",
        user: "${env:DB2_USER}",
        password: "${env:DB2_PASSWORD}",
        database: "${env:DB2_DATABASE}")
    .WithTableIncludeList("SCHEMA.TABLE1", "SCHEMA.TABLE2")
    .WithSnapshotMode("initial")
    .WithJsonConverter()
    .Build();
```

---

## Data Model Changes

No database or external data model changes required. This is purely an infrastructure-as-code refactoring.

---

## Verification Approach

### Build Verification
```bash
cd gitops/applications
dotnet build
```

### Manual Verification
1. Generate manifests with `pulumi up --dry-run`
2. Compare generated YAML with existing manifests
3. Ensure config hash matches for unchanged configurations

### Test Scenarios
1. **PostgreSQL connector** - Should produce identical manifest to existing `PostgresDebeziumConnector`
2. **DB2 connector** - Should produce valid KafkaConnector manifest with DB2-specific config
3. **DD130 naming integration** - Verify auto-derived topic names, connector names, and DLQ topics
4. **Error handling modes** - Verify both fail-fast and DLQ configurations

### Acceptance Criteria
- [x] `NamingConventionHelper` class created and build passes
- [ ] `DebeziumSourceConnectorBuilder` implements fluent builder pattern
- [ ] PostgreSQL preset produces equivalent manifest to current implementation
- [ ] DB2 preset produces valid Debezium DB2 connector configuration
- [ ] DD130 naming convention auto-derives connector names, topics, and DLQ
- [ ] SMT configuration (unwrap, routing) works correctly
- [ ] Both Avro and JSON converters supported
- [ ] Both fail-fast and DLQ error handling modes supported
- [ ] Config hash computation ensures idempotent deployments
- [ ] `PostgresDebeziumConnector` marked as obsolete

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Generated manifest differs from current | Medium | Low | Compare SHA256 hash of generated manifests |
| DB2-specific config missing fields | Low | Medium | Reference Debezium DB2 documentation |
| Breaking change in Strimzi CRD | Low | Medium | Use existing `KafkaConnectorArgs` class |

---

## Dependencies and Blockers

### Prerequisite
- `NamingConventionHelper` class must be created first (currently missing, causing build failures in multiple files)

### No External Dependencies
- All required Pulumi CRDs already exist
- No new NuGet packages needed
