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

Assess the task's difficulty, as underestimating it leads to poor outcomes.
- easy: Straightforward implementation, trivial bug fix or feature
- medium: Moderate complexity, some edge cases or caveats to consider
- hard: Complex logic, many caveats, architectural considerations, or high-risk changes

Create a technical specification for the task that is appropriate for the complexity level:
- Review the existing codebase architecture and identify reusable components.
- Define the implementation approach based on established patterns in the project.
- Identify all source code files that will be created or modified.
- Define any necessary data model, API, or interface changes.
- Describe verification steps using the project's test and lint commands.

Save the output to `{@artifacts_path}/spec.md` with:
- Technical context (language, dependencies)
- Implementation approach
- Source code structure changes
- Data model / API / interface changes
- Verification approach

**Result:** Technical specification saved to `spec.md`. Difficulty assessed as **medium**.

---

### [x] Step: Create NamingConventionHelper (Prerequisite)
<!-- chat-id: 2e3307ac-16b6-4b6b-ba4f-81a363615381 -->

The `NamingConventionHelper` class is referenced throughout the codebase but does not exist, causing build failures.

**Tasks:**
1. Create `gitops/applications/NamingConventionHelper.cs`
2. Implement the `DataLayer` enum (Bronze, Silver, Gold)
3. Implement the `SchemaCompatibility` enum
4. Implement the `TopicComponents` record
5. Implement parsing and generation methods:
   - `ParseTopic(string topicName)`
   - `ToTopicName(TopicComponents)`
   - `ToIcebergTable(TopicComponents)`
   - `ToFlinkJobName(...)`
   - `ToDlqTopic(string sourceTopic)`
   - `GetDefaultCompatibility(DataLayer)`
   - `ToSchemaRegistryString(SchemaCompatibility)`
6. Verify build passes: `dotnet build`

**Verification:**
- `dotnet build` completes without errors
- All existing builders compile successfully

**Result:** `NamingConventionHelper.cs` created with all required enums, record, and methods. Added `global using applications;` to `Program.cs` to fix namespace resolution. Build passes successfully.

---

### [x] Step: Implement DebeziumSourceConnectorBuilder
<!-- chat-id: cb038740-2a27-40de-8618-2004d1e8d57d -->

Create the generic Debezium connector builder following established patterns.

**Tasks:**
1. Create `gitops/applications/kafkaconnect/DebeziumSourceConnectorBuilder.cs`
2. Implement private fields for all configuration options
3. Implement database preset methods:
   - `ForPostgres()` - Sets PostgreSQL connector class and defaults
   - `ForDb2()` - Sets DB2 connector class and defaults
   - `ForMySql()` - Sets MySQL connector class and defaults (placeholder)
4. Implement common configuration methods:
   - `WithConnectorName()`, `WithClusterName()`, `WithTasksMax()`
   - `WithDatabaseConnection()`, `WithDatabaseConnectionFromEnv()`
   - `WithTopicPrefix()`, `WithSnapshotMode()`
   - `WithTableIncludeList()`, `WithTableExcludeList()`
5. Implement PostgreSQL-specific methods:
   - `WithReplicationSlot()`, `WithPublication()`, `WithPlugin()`
6. Implement SMT configuration methods:
   - `WithUnwrap()`, `WithTopicRouting()`
7. Implement Schema Registry methods:
   - `WithSchemaRegistry()`, `WithAvroConverter()`, `WithJsonConverter()`
8. Implement error handling methods:
   - `WithFailFastMode()`, `WithDlqTopic()`
9. Implement DD130 naming integration:
   - `WithNaming()` method
   - Auto-derive connector name, topic prefix, DLQ topic from naming components
10. Implement `Build()` method:
    - Create Pulumi Provider
    - Build configuration dictionary
    - Create KafkaConnector custom resource
    - Compute config hash for idempotency

**Verification:**
- `dotnet build` completes without errors
- Builder follows same pattern as `IcebergSinkConnectorBuilder`

**Result:** `DebeziumSourceConnectorBuilder.cs` created with ~500 lines of code. Implemented:
- Database preset methods: `ForPostgres()`, `ForDb2()`, `ForMySql()`, `ForSqlServer()`, `ForMongoDB()`
- Common configuration: connector name, cluster, tasks, database connection (explicit and from env vars)
- Debezium configuration: topic prefix, snapshot mode, table include/exclude lists
- PostgreSQL-specific: replication slot, publication, plugin name
- SMT configuration: unwrap (ExtractNewRecordState) and topic routing (RegexRouter)
- Schema Registry: Avro and JSON converters with configurable auth
- Error handling: fail-fast mode and DLQ topic
- Performance tuning: batch size, queue size, poll interval
- DD130 naming integration: auto-derives connector name, topic prefix, and DLQ topic
- Build method with config hash computation for idempotent deployments

---

### [ ] Step: Update Program.cs and Deprecate Old Connector

Integrate the new builder and deprecate the old implementation.

**Tasks:**
1. Add `[Obsolete("Use DebeziumSourceConnectorBuilder instead")]` to `PostgresDebeziumConnector` class
2. Update `Program.cs`:
   - Replace `PostgresDebeziumConnector` instantiation with `DebeziumSourceConnectorBuilder`
   - Add example DB2 connector (commented out, for documentation)
3. Verify generated manifests match existing ones

**Verification:**
- Build passes with deprecation warning
- Generated `kafka-connect` manifests are equivalent to existing ones
- Config hash unchanged for identical configuration

---

### [ ] Step: Final Verification and Report

Complete final verification and write implementation report.

**Tasks:**
1. Run `dotnet build` - should pass with only deprecation warning
2. Compare generated manifests:
   - Verify PostgreSQL connector manifest unchanged
   - Verify config-hash annotation identical
3. Write `report.md` documenting:
   - What was implemented
   - How the solution was tested
   - Any issues or challenges encountered

**Verification:**
- All acceptance criteria from spec.md met
- Build passes
- Manifests generated correctly
