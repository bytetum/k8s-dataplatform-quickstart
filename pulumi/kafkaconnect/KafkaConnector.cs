using Pulumi.Kubernetes.ApiExtensions;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Outputs.Core.V1;

namespace Pulumi.Crds.KafkaConnect
{
    public class KafkaConnector : Kubernetes.ApiExtensions.CustomResource
    {
        [Output("spec")] public Output<KafkaConnectorSpec> Spec { get; private set; } = null!;

        public KafkaConnector(string name, KafkaConnectorArgs args, CustomResourceOptions? options = null)
            : base(name, args, options)
        {
        }
    }

    [OutputType]
    public sealed class KafkaConnectorSpec
    {
        [Output("class")] public Output<string> Class { get; private set; } = null!;
        [Output("tasksMax")] public Output<int> TasksMax { get; private set; } = null!;
        [Output("config")] public Output<Dictionary<string, object>> Config { get; private set; } = null!;
    }

    public class KafkaConnectorArgs : CustomResourceArgs
    {
        [Input("spec")] public Input<KafkaConnectorSpecArgs>? Spec { get; set; }

        public KafkaConnectorArgs()
            : base("kafka.strimzi.io/v1beta2", "KafkaConnector")
        {
        }
    }

    public class KafkaConnectorSpecArgs : ResourceArgs
    {
        [Input("class")] public Input<string>? Class { get; set; }
        [Input("tasksMax")] public Input<int>? TasksMax { get; set; }
        [Input("config")] public Input<object>? Config { get; set; } // Accepts Debezium or Iceberg configs
    }

    /// <summary>
    /// Base configuration for all connectors
    /// </summary>
    public abstract class ConnectorConfigBaseArgs : ResourceArgs
    {
        // ==========================================
        // Common / Converters
        // ==========================================
        [Input("key.converter")] public Input<string>? KeyConverter { get; set; }
        [Input("value.converter")] public Input<string>? ValueConverter { get; set; }
        [Input("key.converter.schemas.enable")] public Input<bool>? KeyConverterSchemasEnable { get; set; }
        [Input("value.converter.schemas.enable")] public Input<bool>? ValueConverterSchemasEnable { get; set; }

        // ==========================================
        // Transforms
        // ==========================================
        [Input("transforms")] public Input<string>? Transforms { get; set; }
        [Input("transforms.route.type")] public Input<string>? TransformsRouteType { get; set; }
        [Input("transforms.route.regex")] public Input<string>? TransformsRouteRegex { get; set; }
        [Input("transforms.route.replacement")] public Input<string>? TransformsRouteReplacement { get; set; }
        [Input("transforms.unwrap.type")] public Input<string>? TransformsUnwrapType { get; set; }
        [Input("transforms.unwrap.drop.tombstones")] public Input<bool>? TransformsUnwrapDropTombstones { get; set; }
        [Input("transforms.unwrap.delete.handling.mode")] public Input<string>? TransformsUnwrapDeleteHandlingMode { get; set; }
        [Input("transforms.unwrap.add.fields")] public Input<string>? TransformsUnwrapAddFields { get; set; }

        // ==========================================
        // Error Handling
        // ==========================================
        [Input("errors.tolerance")] public Input<string>? ErrorsTolerance { get; set; }
        [Input("errors.deadletterqueue.topic.name")] public Input<string>? ErrorsDeadLetterQueueTopicName { get; set; }
        [Input("errors.deadletterqueue.context.headers.enable")] public Input<bool>? ErrorsDeadLetterQueueContextHeadersEnable { get; set; }
    }

    /// <summary>
    /// Configuration for Debezium CDC Connectors (Postgres, MySQL, etc.)
    /// </summary>
    public class DebeziumConnectorConfigArgs : ConnectorConfigBaseArgs
    {
        [Input("topic.prefix")] public Input<string>? TopicPrefix { get; set; }
        [Input("snapshot.mode")] public Input<string>? SnapshotMode { get; set; }
        [Input("plugin.name")] public Input<string>? PluginName { get; set; }   
        [Input("slot.name")] public Input<string>? SlotName { get; set; }
        [Input("publication.name")] public Input<string>? PublicationName { get; set; }
        [Input("table.include.list")] public Input<string>? TableIncludeList { get; set; }

        // Database Connection (Generic for various DBs)
        [Input("database.hostname")] public Input<string>? DatabaseHostname { get; set; }
        [Input("database.port")] public Input<string>? DatabasePort { get; set; }
        [Input("database.user")] public Input<string>? DatabaseUser { get; set; }
        [Input("database.password")] public Input<string>? DatabasePassword { get; set; }
        [Input("database.dbname")] public Input<string>? DatabaseDbname { get; set; }
        [Input("database.server.name")] public Input<string>? DatabaseServerName { get; set; }
        [Input("database.server.id")] public Input<int>? DatabaseServerId { get; set; }
        
        // MongoDB specific
        [Input("mongodb.connection.string")] public Input<string>? MongodbConnectionString { get; set; }
        [Input("mongodb.user")] public Input<string>? MongodbUser { get; set; }
        [Input("mongodb.password")] public Input<string>? MongodbPassword { get; set; }
    }

    /// <summary>
    /// Configuration for Iceberg Sink Connector
    /// </summary>
    public class IcebergConnectorConfigArgs : ConnectorConfigBaseArgs
    {
        [Input("topics")] public Input<string>? Topics { get; set; }
        [Input("iceberg.tables")] public Input<string>? IcebergTables { get; set; }
        [Input("iceberg.tables.auto-create-enabled")] public Input<bool>? IcebergTablesAutoCreateEnabled { get; set; }
        [Input("iceberg.tables.evolve-schema-enabled")] public Input<bool>? IcebergTablesEvolveSchemaEnabled { get; set; }
        [Input("iceberg.tables.upsert-mode-enabled")] public Input<bool>? IcebergTablesUpsertModeEnabled { get; set; }
        [Input("iceberg.tables.default-id-columns")] public Input<string>? IcebergTablesDefaultIdColumns { get; set; }
        
        [Input("iceberg.catalog")] public Input<string>? IcebergCatalog { get; set; }
        [Input("iceberg.catalog.type")] public Input<string>? IcebergCatalogType { get; set; }
        [Input("iceberg.catalog.warehouse")] public Input<string>? IcebergCatalogWarehouse { get; set; }
        [Input("iceberg.catalog.catalog-name")] public Input<string>? IcebergCatalogCatalogName { get; set; }
        [Input("iceberg.catalog.uri")] public Input<string>? IcebergCatalogUri { get; set; }
        [Input("iceberg.catalog.oauth2-server-uri")] public Input<string>? IcebergCatalogOauth2ServerUri { get; set; }
        [Input("iceberg.catalog.credential")] public Input<string>? IcebergCatalogCredential { get; set; }
        [Input("iceberg.catalog.scope")] public Input<string>? IcebergCatalogScope { get; set; }

        [Input("iceberg.control.commit.interval-ms")] public Input<int>? IcebergControlCommitIntervalMs { get; set; }
        [Input("iceberg.control.commit.timeout-ms")] public Input<int>? IcebergControlCommitTimeoutMs { get; set; }
        [Input("iceberg.control.commit.threads")] public Input<int>? IcebergControlCommitThreads { get; set; }
    }
}
