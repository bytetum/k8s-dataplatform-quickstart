using System.Collections.Generic;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace applications.kafkaconnect;

internal class PostgresDebeziumConnector : ComponentResource
{
    public PostgresDebeziumConnector(string manifestsRoot) : base(
        "postgres-debezium-connector",
        "postgres-debezium-connector")
    {
        var provider = new Kubernetes.Provider("yaml-provider", new()
        {
            RenderYamlToDirectory = $"{manifestsRoot}/kafka-connect"
        }, new CustomResourceOptions
        {
            Parent = this
        });

        var postgresDebeziumConnector = new Kubernetes.ApiExtensions.CustomResource("postgres-debezium-connector",
            new KafkaConnectorArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "postgres-debezium-source",
                    Namespace = "kafka-connect",
                    Labels = new Dictionary<string, string>
                    {
                        { "strimzi.io/cluster", "universal-data-pipeline-connect" }
                    }
                },
                Spec = new Dictionary<string, object>
                {
                    ["class"] = "io.debezium.connector.postgresql.PostgresConnector",
                    ["tasksMax"] = 1,
                    ["config"] = new Dictionary<string, object>
                    {
                        // Database Connection
                        ["database.hostname"] = "${env:POSTGRES_HOST}",
                        ["database.port"] = "${env:POSTGRES_PORT}",
                        ["database.user"] = "${env:POSTGRES_USER}",
                        ["database.password"] = "${env:POSTGRES_PASSWORD}",
                        ["database.dbname"] = "${env:POSTGRES_DB}",

                        // Debezium Configuration
                        ["topic.prefix"] = "postgres-cdc",
                        ["table.include.list"] = "public.employees",
                        ["plugin.name"] = "pgoutput",
                        ["slot.name"] = "debezium_test_slot",
                        ["publication.name"] = "dbz_publication",

                        // Snapshot Configuration
                        ["snapshot.mode"] = "always",

                        // Kafka Converters
                        ["key.converter"] = "org.apache.kafka.connect.json.JsonConverter",
                        ["value.converter"] = "org.apache.kafka.connect.json.JsonConverter",
                        ["key.converter.schemas.enable"] = true,
                        ["value.converter.schemas.enable"] = true,

                        // Transforms
                        ["transforms"] = "route,unwrap",

                        // Route Transform - redirects messages to bronze-topic
                        ["transforms.route.type"] = "org.apache.kafka.connect.transforms.RegexRouter",
                        ["transforms.route.regex"] = "postgres-cdc.public.employees",
                        ["transforms.route.replacement"] = "bronze.postgres.employees",

                        // Unwrap Transform - extracts clean records from CDC envelope
                        ["transforms.unwrap.type"] = "io.debezium.transforms.ExtractNewRecordState",
                        ["transforms.unwrap.drop.tombstones"] = false,
                        ["transforms.unwrap.delete.handling.mode"] = "rewrite",
                        ["transforms.unwrap.add.fields"] = "timestamp",

                        // Error Handling
                        ["errors.tolerance"] = "all",
                        ["errors.deadletterqueue.topic.name"] = "debezium-errors",
                        ["errors.deadletterqueue.context.headers.enable"] = true
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
