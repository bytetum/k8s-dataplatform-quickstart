# ARM64 Kafka Connect image

`Dockerfile` creates a `linux/arm64` Kafka Connect image based on Strimzi
`0.47.0` / Kafka `4.0.0`. It installs only the plugins required by the checked-in
connectors:

- Debezium PostgreSQL `3.0.0.Final` (`io.debezium.connector.postgresql.PostgresConnector`)
- Apache Iceberg Kafka Connect runtime `1.7.1` (`org.apache.iceberg.connect.IcebergSinkConnector`), built from an exact upstream release commit
- Confluent Avro converter and its Schema Registry client

No credentials, registry endpoints, or secret material are part of the build.
The Avro converter is copied from Confluent's Connect image because the converter
and Schema Registry client must remain a compatible dependency set.

Build a local image (no push):

```bash
bash images/kafka-connect/build-arm64.sh local/kafka-connect:0.47.0-kafka-4.0.0-arm64
```

Before changing the KafkaConnect resource, confirm the image contains the three
classes above using the Connect REST plugin endpoint in a disposable deployment.
For Kind, separately load the built image into the intended cluster and use a
non-expiring image reference. Do not substitute a `ttl.sh` tag.

The base tag is intentionally pinned to the requested Strimzi/Kafka version.
If the selected Strimzi operator does not support Kafka `4.0.0`, update the two
build arguments together to a supported release pair; do not silently fall back
to the old amd64 image.
