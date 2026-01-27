using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pulumi.Crds;
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

        var schemaRegistryUrl = "http://warpstream-schema-registry-warpstream-agent.warpstream.svc.cluster.local:9094";
        var schemaRegistryAuth = "${env:SCHEMA_REGISTRY_USERNAME}:${env:SCHEMA_REGISTRY_PASSWORD}";

        var config = new Dictionary<string, object>
        {
            // Database connection
            ["database.hostname"] = "${env:POSTGRES_HOST}",
            ["database.port"] = "${env:POSTGRES_PORT}",
            ["database.user"] = "${env:POSTGRES_USER}",
            ["database.password"] = "${env:POSTGRES_PASSWORD}",
            ["database.dbname"] = "${env:POSTGRES_DB}",

            // Connector identity
            ["topic.prefix"] = "m3-cdc",

            // Snapshot strategy
            ["snapshot.mode"] = "always",

            // Table selection
            ["table.include.list"] = "public.CSYTAB,public.CIDMAS,public.CIDVEN",

            // Replication configuration
            ["plugin.name"] = "pgoutput",
            ["publication.name"] = "dbz_m3_publication",
            ["slot.name"] = "m3_debezium_slot",

            // SMT chain (Single Message Transforms)
            ["transforms"] = "unwrap,route",

            ["transforms.unwrap.type"] = "io.debezium.transforms.ExtractNewRecordState",
            ["transforms.unwrap.add.fields"] = "op,source.ts_ms",
            ["transforms.unwrap.add.headers"] = "op,source.ts_ms",
            ["transforms.unwrap.delete.handling.mode"] = "rewrite",
            ["transforms.unwrap.drop.tombstones"] = true,

            ["transforms.route.type"] = "org.apache.kafka.connect.transforms.RegexRouter",
            ["transforms.route.regex"] = "m3-cdc.public.(.*)",
            ["transforms.route.replacement"] = "bronze.m3.$1",

            // Schema handling (Avro with Schema Registry)
            ["key.converter"] = "io.confluent.connect.avro.AvroConverter",
            ["key.converter.schema.registry.url"] = schemaRegistryUrl,
            ["key.converter.schema.registry.basic.auth.user.info"] = schemaRegistryAuth,
            ["key.converter.schema.registry.basic.auth.credentials.source"] = "USER_INFO",
            ["key.converter.schemas.enable"] = true,

            ["value.converter"] = "io.confluent.connect.avro.AvroConverter",
            ["value.converter.schema.registry.url"] = schemaRegistryUrl,
            ["value.converter.schema.registry.basic.auth.user.info"] = schemaRegistryAuth,
            ["value.converter.schema.registry.basic.auth.credentials.source"] = "USER_INFO",
            ["value.converter.schemas.enable"] = true,

            // Error handling
            ["errors.tolerance"] = "all",
            ["errors.deadletterqueue.topic.name"] = "m3-debezium-errors",
            ["errors.deadletterqueue.context.headers.enable"] = true,

            // Performance tuning
            ["max.batch.size"] = 2048,
            ["max.queue.size"] = 8192,
            ["poll.interval.ms"] = 1000
        };

        var postgresDebeziumConnector = new Kubernetes.ApiExtensions.CustomResource("postgres-debezium-connector",
            new KafkaConnectorArgs()
            {
                Metadata = new ObjectMetaArgs
                {
                    Name = "postgres-debezium-source",
                    Namespace = "kafka-connect",
                    Labels = new Dictionary<string, string>
                    {
                        { "strimzi.io/cluster", "m3-kafka-connect" }
                    },
                    Annotations = new Dictionary<string, string>
                    {
                        { "config-hash", ComputeConfigHash(config) }
                    }
                },
                Spec = new Dictionary<string, object>
                {
                    ["class"] = "io.debezium.connector.postgresql.PostgresConnector",
                    ["tasksMax"] = 1,
                    ["config"] = config
                }
            }, new CustomResourceOptions
            {
                Provider = provider,
                Parent = this
            });
    }

    private static string ComputeConfigHash(Dictionary<string, object> config)
    {
        var json = JsonSerializer.Serialize(config);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }

    private class KafkaConnectorArgs : Kubernetes.ApiExtensions.CustomResourceArgs
    {
        public KafkaConnectorArgs() : base("kafka.strimzi.io/v1beta2", "KafkaConnector")
        {
        }

        [Input("spec")] public Dictionary<string, object>? Spec { get; set; }
    }
}
