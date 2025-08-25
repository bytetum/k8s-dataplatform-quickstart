-- This script is a template. Values are injected by Flink's configuration.

CREATE TABLE input_source (
    `message` STRING
) WITH (
    'connector' = 'kafka',
    'topic' = '${kafka.input.topic}',
    'properties.bootstrap.servers' = '${kafka.bootstrap.servers}',
    'scan.startup.mode' = 'latest-offset',
    'format' = 'raw',
    'properties.security.protocol' = 'SASL_PLAINTEXT',
    'properties.sasl.mechanism' = 'PLAIN',
    'properties.sasl.jaas.config' = '${kafka.sasl.jaas.config}'
);

CREATE TABLE output_sink (
    `message` STRING
) WITH (
    'connector' = 'kafka',
    'topic' = '${kafka.output.topic}',
    'properties.bootstrap.servers' = '${kafka.bootstrap.servers}',
    'format' = 'raw',
    'properties.security.protocol' = 'SASL_PLAINTEXT',
    'properties.sasl.mechanism' = 'PLAIN',
    'properties.sasl.jaas.config' = '${kafka.sasl.jaas.config}'
);

INSERT INTO output_sink
SELECT
    'PROCESSED VIA CONFIGMAP: ' || UPPER(`message`)
FROM
    input_source;