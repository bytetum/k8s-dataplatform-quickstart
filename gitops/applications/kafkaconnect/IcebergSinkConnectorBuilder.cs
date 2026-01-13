using System.Collections.Generic;
using Pulumi;
using Pulumi.Crds.KafkaConnect;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

/// <summary>
/// Builder for creating Iceberg Sink Connectors with sensible defaults.
/// Only requires table-specific configuration; all common settings are pre-configured.
/// </summary>
public class IcebergSinkConnectorBuilder
{
    private string _manifestsRoot = "";
    private string _connectorName = "";
    private string _sourceTopic = "";
    private string _destinationTable = "";
    private string? _idColumns;
    private string? _partitionBy;
    private string _clusterName = "m3-kafka-connect";
    private int _tasksMax = 1;

    // Common defaults (can be overridden)
    private string _schemaRegistryUrl =
        "http://warpstream-schema-registry-warpstream-agent.warpstream.svc.cluster.local:9094";

    private string _schemaRegistryAuth =
        "ccun_291350ada8541780bdbc5663f2d22855a4da5bf905a576bac6c8dfa95c89db71:ccp_956975877bc5eeb62ce21d18c49d320a3d128cb9d0c81278999a742f6272090e";

    private string _polarisUri = "http://polaris.polaris.svc.cluster.local:8181/api/catalog";
    private string _catalogName = "ao_catalog";
    private string _dlqTopic = "m3.iceberg.dlq";

    /// <summary>
    /// Create a new Iceberg Sink Connector builder.
    /// </summary>
    /// <param name="manifestsRoot">Root directory for generated manifests</param>
    public IcebergSinkConnectorBuilder(string manifestsRoot)
    {
        _manifestsRoot = manifestsRoot;
    }

    /// <summary>
    /// Set the connector name (required).
    /// </summary>
    /// <param name="connectorName">Unique name for this connector (e.g., "cidmas")</param>
    public IcebergSinkConnectorBuilder WithConnectorName(string connectorName)
    {
        _connectorName = connectorName;
        return this;
    }

    /// <summary>
    /// Set the source Kafka topic (required).
    /// </summary>
    /// <param name="sourceTopic">Source Kafka topic (e.g., "bronze.m3.cidmas")</param>
    public IcebergSinkConnectorBuilder WithSourceTopic(string sourceTopic)
    {
        _sourceTopic = sourceTopic;
        return this;
    }

    /// <summary>
    /// Set the destination Iceberg table (required).
    /// </summary>
    /// <param name="destinationTable">Destination Iceberg table (e.g., "m3_bronze.cidmas")</param>
    public IcebergSinkConnectorBuilder WithDestinationTable(string destinationTable)
    {
        _destinationTable = destinationTable;
        return this;
    }

    /// <summary>
    /// Set the ID columns for upsert mode (comma-separated).
    /// Example: "idcono,idsuno" or "iisuno"
    /// </summary>
    public IcebergSinkConnectorBuilder WithIdColumns(params string[] idColumns)
    {
        _idColumns = string.Join(",", idColumns);
        return this;
    }

    /// <summary>
    /// Set the partition column for the Iceberg table.
    /// Example: "idcono" or "iisugr"
    /// </summary>
    public IcebergSinkConnectorBuilder WithPartitionBy(string partitionColumn)
    {
        _partitionBy = partitionColumn;
        return this;
    }

    /// <summary>
    /// Set the Kafka Connect cluster name (default: "m3-kafka-connect")
    /// </summary>
    public IcebergSinkConnectorBuilder WithClusterName(string clusterName)
    {
        _clusterName = clusterName;
        return this;
    }

    /// <summary>
    /// Set the maximum number of tasks (default: 1)
    /// </summary>
    public IcebergSinkConnectorBuilder WithTasksMax(int tasksMax)
    {
        _tasksMax = tasksMax;
        return this;
    }

    /// <summary>
    /// Override the schema registry URL
    /// </summary>
    public IcebergSinkConnectorBuilder WithSchemaRegistry(string url, string auth)
    {
        _schemaRegistryUrl = url;
        _schemaRegistryAuth = auth;
        return this;
    }

    /// <summary>
    /// Override the DLQ topic name (default: "m3.iceberg.dlq")
    /// </summary>
    public IcebergSinkConnectorBuilder WithDlqTopic(string dlqTopic)
    {
        _dlqTopic = dlqTopic;
        return this;
    }

    /// <summary>
    /// Build and create the KafkaConnector resource.
    /// </summary>
    public ComponentResource Build()
    {
        var componentResource =
            new ComponentResource($"m3-iceberg-sink-{_connectorName}", $"m3-iceberg-sink-{_connectorName}");

        var provider = new Pulumi.Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{_manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = componentResource
        });
        var config = new Dictionary<string, object>
        {
            // ========================================================================
            // 1. SOURCE TOPIC
            // ========================================================================
            ["topics"] = _sourceTopic,

            // ========================================================================
            // 2. DESTINATION TABLE
            // ========================================================================
            ["iceberg.tables"] = _destinationTable,

            // ========================================================================
            // 4. GLOBAL TABLE SETTINGS
            // ========================================================================
            ["iceberg.tables.auto-create-enabled"] = true,
            ["iceberg.tables.upsert-mode-enabled"] = true,
            ["iceberg.tables.evolve-schema-enabled"] = true,
            ["iceberg.tables.schema-force-optional"] = true,

            // ========================================================================
            // 5. ICEBERG CATALOG CONNECTION (Polaris)
            // ========================================================================
            ["iceberg.catalog"] = "iceberg",
            ["iceberg.catalog.type"] = "rest",
            ["iceberg.catalog.catalog-name"] = _catalogName,
            ["iceberg.catalog.uri"] = _polarisUri,
            ["iceberg.catalog.credential"] = "root:${env:POLARIS_PASSWORD}",
            ["iceberg.catalog.warehouse"] = _catalogName,
            ["iceberg.catalog.scope"] = "PRINCIPAL_ROLE:ALL",
            ["iceberg.catalog.oauth2-server-uri"] = $"{_polarisUri}/v1/oauth/tokens",

            // ========================================================================
            // 6. SCHEMA REGISTRY & SERIALIZATION
            // ========================================================================
            ["key.converter"] = "io.confluent.connect.avro.AvroConverter",
            ["key.converter.schema.registry.url"] = _schemaRegistryUrl,
            ["key.converter.schema.registry.basic.auth.user.info"] = _schemaRegistryAuth,
            ["key.converter.schema.registry.basic.auth.credentials.source"] = "USER_INFO",
            ["key.converter.schemas.enable"] = true,

            ["value.converter"] = "io.confluent.connect.avro.AvroConverter",
            ["value.converter.schema.registry.url"] = _schemaRegistryUrl,
            ["value.converter.schema.registry.basic.auth.user.info"] = _schemaRegistryAuth,
            ["value.converter.schema.registry.basic.auth.credentials.source"] = "USER_INFO",
            ["value.converter.schemas.enable"] = true,

            // ========================================================================
            // 7. COMMIT COORDINATION & PERFORMANCE
            // ========================================================================
            ["iceberg.control.commit.interval-ms"] = 60000,
            ["iceberg.control.commit.threads"] = 2,
            ["iceberg.control.commit.timeout-ms"] = 30000,
            ["iceberg.control.topic"] = $"sink-control-iceberg-{_connectorName}",
            ["iceberg.control.group-id-prefix"] = $"m3-iceberg-{_connectorName}-control",

            // ========================================================================
            // 8. ERROR HANDLING
            // ========================================================================
            ["errors.tolerance"] = "all",
            ["errors.log.enable"] = true,
            ["errors.deadletterqueue.topic.name"] = _dlqTopic,
            ["errors.deadletterqueue.context.headers.enable"] = true,
        };

        // ========================================================================
        // 3. PRIMARY KEYS & PARTITIONING (table-specific)
        // ========================================================================
        if (!string.IsNullOrEmpty(_idColumns))
        {
            config[$"iceberg.table.{_destinationTable}.id-columns"] = _idColumns;
        }

        if (!string.IsNullOrEmpty(_partitionBy))
        {
            config[$"iceberg.table.{_destinationTable}.partition-by"] = _partitionBy;
        }


        new KafkaConnector($"m3-iceberg-sink-{_connectorName}", new KafkaConnectorArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = $"m3-iceberg-sink-{_connectorName}",
                Namespace = "kafka-connect",
                Labels = new Dictionary<string, string>
                {
                    { "strimzi.io/cluster", _clusterName }
                }
            },
            Spec = new KafkaConnectorSpecArgs
            {
                Class = "io.tabular.iceberg.connect.IcebergSinkConnector",
                TasksMax = _tasksMax,
                Config = config
            }
        }, new CustomResourceOptions
        {
            Provider = provider,
            Parent = componentResource
        });

        return componentResource;
    }
}