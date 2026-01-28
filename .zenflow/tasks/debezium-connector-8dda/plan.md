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

### [x] Step: Create DebeziumSourceConnectorBuilder Core Structure
<!-- chat-id: dcf14d8b-74c8-435c-af5c-9ba1dc99d142 -->

Create the new builder class with:
- [x] Define `DatabaseType` enum (Postgres, Db2, MySQL, SqlServer, Oracle, MongoDB)
- [x] Define `SnapshotMode` enum (Initial, Always, Never, SchemaOnly, WhenNeeded)
- [x] Define `DeleteHandlingMode` enum (None, Rewrite, Drop)
- [x] Create builder class with private fields for all configuration options
- [x] Implement constructor accepting `manifestsRoot` parameter
- [x] Implement `WithDatabaseType()` method
- [x] Implement `WithDatabaseConnection()` method for common DB credentials

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [x] Step: Add DD130 Naming and Common Configuration Methods
<!-- chat-id: 1a228798-bc12-47ff-97bf-993e0c4e52ec -->

Add builder methods for:
- [x] `WithNaming()` - DD130 naming convention integration (layer, domain, dataset)
- [x] `WithTopicPrefix()` - Alternative explicit topic prefix
- [x] `WithConnectorName()` - Override auto-generated connector name
- [x] `WithTableIncludeList()` / `WithTableExcludeList()` - Table selection
- [x] `WithSnapshotMode()` - Snapshot behavior control
- [x] `WithClusterName()` - Strimzi cluster label
- [x] `WithTasksMax()` - Connector parallelism

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [x] Step: Add Database-Specific Configuration Methods
<!-- chat-id: b676152f-b77d-406e-a626-465b2db8dae0 -->

Implement database-specific methods:
- [x] `WithPostgresReplication(publicationName, slotName, pluginName)` - PostgreSQL logical replication
- [x] `WithDb2Asn(asnProgram, asnLib)` - DB2 ASN capture configuration
- [x] `WithMySqlServerId(serverId)` - MySQL binlog server ID
- [x] `WithMongoDbConnectionString(connectionString)` - MongoDB connection

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [x] Step: Add Transform, Schema, and Error Handling Methods
<!-- chat-id: 4ad378bb-cc0e-45e8-87de-d518bd736a80 -->

Implement:
- [x] `WithUnwrapTransform()` - ExtractNewRecordState SMT configuration
- [x] `WithRouteTransform()` - RegexRouter SMT for topic routing
- [x] `WithSchemaRegistry()` - Schema registry URL and auth
- [x] `WithAvroConverter()` / `WithJsonConverter()` - Serialization format
- [x] `WithErrorTolerance()` - Error handling mode
- [x] `WithDeadLetterQueue()` - DLQ topic configuration
- [x] `WithPerformanceTuning()` - Batch size, queue size, poll interval

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [x] Step: Implement Build Method and Connector Class Mapping
<!-- chat-id: 55c76dc6-28a6-4474-9f09-663ed3aa51fd -->

Implement the `Build()` method:
- [x] Map `DatabaseType` to Debezium connector class name
- [x] Apply DD130 naming if configured (derive topic prefix, connector name, DLQ)
- [x] Build configuration dictionary with all settings
- [x] Add database-specific configuration based on type
- [x] Create Pulumi provider for YAML rendering
- [x] Create `KafkaConnector` custom resource
- [x] Compute config hash for annotations
- [x] Return `ComponentResource`

**File**: `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`

**Verification**: `dotnet build` in `gitops/applications/`

---

### [x] Step: Migrate Program.cs and Verify Output
<!-- chat-id: 187ba788-ebb9-4d24-88ff-2e75ee1435ad -->

Update Program.cs to use the new builder:
- [x] Replace `PostgresDebeziumConnector` instantiation with `DebeziumSourceConnectorBuilder`
- [x] Configure to match existing PostgreSQL CDC setup exactly
- [x] Run `dotnet run` to generate manifests
- [x] Compare generated YAML with existing `postgres-debezium-source` manifest
- [x] Ensure no functional changes in output
- [x] Mark `PostgresDebeziumConnector` as `[Obsolete]` or delete

**Files**:
- `gitops/applications/Program.cs`
- `gitops/applications/kafkaconnect/PostgresDebeziumConnector.cs` (deleted)

**Verification**:
```bash
cd gitops/applications
dotnet build
dotnet run
# Compare: gitops/manifests/kafka-connect/*.yaml
```

**Completed**: Migration successful. The new builder generates the same YAML manifest as the original `PostgresDebeziumConnector`. The old file was deleted and the build passes.

---

### [ ] Step: Final Report

Write implementation report to `.zenflow/tasks/debezium-connector-8dda/report.md`:
- What was implemented
- How the solution was tested
- Any issues or challenges encountered
