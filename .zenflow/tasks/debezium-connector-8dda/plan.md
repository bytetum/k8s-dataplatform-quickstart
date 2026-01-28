# Spec and build

## Configuration
- **Artifacts Path**: {@artifacts_path} → `.zenflow/tasks/{task_id}`

---

## Agent Instructions

Ask the user questions when anything is unclear or needs their input. This includes:
- Ambiguous or incomplete requirements
- Technical decisions that affect architecture or user experience
- Trade-offs that require business context

Do not make assumptions on important decisions — get clarification first.

---

## Workflow Steps

### [x] Step: Technical Specification

**Difficulty**: Medium

Technical specification created at `.zenflow/tasks/debezium-connector-8dda/spec.md`.

Summary:
- Refactor `PostgresDebeziumConnector` into generic `DebeziumSourceConnectorBuilder`
- Follow existing `IcebergSinkConnectorBuilder` pattern (fluent builder)
- Support multiple database types: PostgreSQL, DB2, MySQL, SqlServer, Oracle, MongoDB
- Integrate with DD130 naming conventions via `NamingConventionHelper`
- Provide database-specific configuration methods

---

### [ ] Step: Create DebeziumSourceConnectorBuilder Core Structure

Create the new builder class with:
- [ ] Define `DatabaseType` enum (Postgres, Db2, MySQL, SqlServer, Oracle, MongoDB)
- [ ] Define `SnapshotMode` enum (Initial, Always, Never, SchemaOnly, WhenNeeded)
- [ ] Define `DeleteHandlingMode` enum (None, Rewrite, Drop)
- [ ] Create builder class with private fields for all configuration options
- [ ] Implement constructor accepting `manifestsRoot` parameter
- [ ] Implement `WithDatabaseType()` method
- [ ] Implement `WithDatabaseConnection()` method for common DB credentials

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [ ] Step: Add DD130 Naming and Common Configuration Methods

Add builder methods for:
- [ ] `WithNaming()` - DD130 naming convention integration (layer, domain, dataset)
- [ ] `WithTopicPrefix()` - Alternative explicit topic prefix
- [ ] `WithConnectorName()` - Override auto-generated connector name
- [ ] `WithTableIncludeList()` / `WithTableExcludeList()` - Table selection
- [ ] `WithSnapshotMode()` - Snapshot behavior control
- [ ] `WithClusterName()` - Strimzi cluster label
- [ ] `WithTasksMax()` - Connector parallelism

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [ ] Step: Add Database-Specific Configuration Methods

Implement database-specific methods:
- [ ] `WithPostgresReplication(publicationName, slotName, pluginName)` - PostgreSQL logical replication
- [ ] `WithDb2Asn(asnProgram, asnLib)` - DB2 ASN capture configuration
- [ ] `WithMySqlServerId(serverId)` - MySQL binlog server ID
- [ ] `WithMongoDbConnectionString(connectionString)` - MongoDB connection

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [ ] Step: Add Transform, Schema, and Error Handling Methods

Implement:
- [ ] `WithUnwrapTransform()` - ExtractNewRecordState SMT configuration
- [ ] `WithRouteTransform()` - RegexRouter SMT for topic routing
- [ ] `WithSchemaRegistry()` - Schema registry URL and auth
- [ ] `WithAvroConverter()` / `WithJsonConverter()` - Serialization format
- [ ] `WithErrorTolerance()` - Error handling mode
- [ ] `WithDeadLetterQueue()` - DLQ topic configuration
- [ ] `WithPerformanceTuning()` - Batch size, queue size, poll interval

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [ ] Step: Implement Build Method and Connector Class Mapping

Implement the `Build()` method:
- [ ] Map `DatabaseType` to Debezium connector class name
- [ ] Apply DD130 naming if configured (derive topic prefix, connector name, DLQ)
- [ ] Build configuration dictionary with all settings
- [ ] Add database-specific configuration based on type
- [ ] Create Pulumi provider for YAML rendering
- [ ] Create `KafkaConnector` custom resource
- [ ] Compute config hash for annotations
- [ ] Return `ComponentResource`

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [ ] Step: Migrate Program.cs and Verify Output

Update Program.cs to use the new builder:
- [ ] Replace `PostgresDebeziumConnector` instantiation with `DebeziumSourceConnectorBuilder`
- [ ] Configure to match existing PostgreSQL CDC setup exactly
- [ ] Run `dotnet run` to generate manifests
- [ ] Compare generated YAML with existing `postgres-debezium-source` manifest
- [ ] Ensure no functional changes in output
- [ ] Mark `PostgresDebeziumConnector` as `[Obsolete]` or delete

**Files**:
- `gitops/applications/Program.cs`
- `gitops/applications/kafkaconnect/PostgresDebeziumConnector.cs` (deprecate/delete)

**Verification**:
```bash
cd gitops/applications
dotnet build
dotnet run
# Compare: gitops/manifests/kafka-connect/*.yaml
```

---

### [ ] Step: Final Report

Write implementation report to `.zenflow/tasks/debezium-connector-8dda/report.md`:
- What was implemented
- How the solution was tested
- Any issues or challenges encountered
