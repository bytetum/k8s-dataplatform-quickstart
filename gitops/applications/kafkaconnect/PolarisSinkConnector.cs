using System.Collections.Generic;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

internal class PolarisSinkConnector : ComponentResource
{
    public PolarisSinkConnector(string manifestsRoot) : base(
        "polaris-sink-connector",
        "polaris-sink-connector")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = this
        });

        var polarisSinkConnector = new Kubernetes.ApiExtensions.CustomResource("polaris-sink-connector",
            new KafkaConnectorArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "polaris-sink-connector",
                    Namespace = "kafka-connect",
                    Labels = new Dictionary<string, string>
                    {
                        { "strimzi.io/cluster", "universal-data-pipeline-connect" }
                    }
                },
                Spec = new Dictionary<string, object>
                {
                    ["class"] = "io.tabular.iceberg.connect.IcebergSinkConnector",
                    ["tasksMax"] = 1,
                    ["config"] = new Dictionary<string, object>
                    {
                        // Kafka Topics and Converters
                        ["topics"] = "bronze-topic",
                        ["key.converter"] = "org.apache.kafka.connect.json.JsonConverter",
                        ["value.converter"] = "org.apache.kafka.connect.json.JsonConverter",
                        ["key.converter.schemas.enable"] = true,
                        ["value.converter.schemas.enable"] = true,

                        // Iceberg Table Settings
                        ["iceberg.tables.auto-create-enabled"] = true,
                        // NOTE: test_db database/namespace must be created in Polaris before deploying this connector
                        // Create via Spark SQL: CREATE DATABASE test_db
                        // Or via Polaris API: POST /api/catalog/v1/ao_catalog/namespaces
                        ["iceberg.tables"] = "test_db.users",
                        ["iceberg.tables.upsert-mode-enabled"] = false,
                        ["iceberg.tables.evolve-schema-enabled"] = "true",

                        // Iceberg Catalog Configuration
                        ["iceberg.catalog"] = "iceberg",
                        ["iceberg.catalog.type"] = "rest",
                        ["iceberg.catalog.warehouse"] = "ao_catalog",
                        ["iceberg.catalog.catalog-name"] = "ao_catalog",
                        ["iceberg.catalog.uri"] = "http://polaris.polaris.svc.cluster.local:8181/api/catalog",
                        ["iceberg.catalog.oauth2-server-uri"] = "http://polaris.polaris.svc.cluster.local:8181/api/catalog/v1/oauth/tokens",
                        ["iceberg.catalog.credential"] = "root:${env:POLARIS_PASSWORD}",
                        ["iceberg.catalog.scope"] = "PRINCIPAL_ROLE:ALL",

                        // Iceberg Control Settings
                        ["iceberg.control.commit.interval-ms"] = "300000",
                        ["iceberg.control.commit.timeout-ms"] = "30000",
                        ["iceberg.control.commit.threads"] = "8",

                        // AWS/S3 Configuration
                        ["iceberg.catalog.s3.region"] = "${env:AWS_REGION}",
                        ["iceberg.catalog.client.region"] = "${env:AWS_REGION}",
                        ["iceberg.hadoop.fs.s3a.access.key"] = "${env:AWS_ACCESS_KEY_ID}",
                        ["iceberg.hadoop.fs.s3a.secret.key"] = "${env:AWS_SECRET_ACCESS_KEY}",

                        // Error Handling
                        ["errors.tolerance"] = "all",
                        ["errors.deadletterqueue.topic.name"] = "polaris-sink-errors",
                        ["errors.deadletterqueue.context.headers.enable"] = "true"
                    }
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });
    }

    private class KafkaConnectorArgs : Kubernetes.ApiExtensions.CustomResourceArgs
    {
        public KafkaConnectorArgs() : base("kafka.strimzi.io/v1beta2", "KafkaConnector")
        {
        }

        [Input("spec")] public Dictionary<string, object>? Spec { get; set; }
    }
}