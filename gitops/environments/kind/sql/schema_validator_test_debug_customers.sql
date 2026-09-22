SET 'pipeline.name' = 'kind-local-customer-seed';

CREATE TABLE customers (
  record_key STRING,
  customer_id STRING,
  customer_name STRING,
  iceberg_table STRING
) WITH (
  'connector' = 'kafka',
  'topic' = 'kind-local.debug.silver.m3.customers',
  'properties.bootstrap.servers' = 'warpstream-agent.lakehouse-warpstream.svc.cluster.local:9092',
  'format' = 'avro-confluent',
  'avro-confluent.url' = 'http://warpstream-schema-registry-warpstream-agent.lakehouse-warpstream.svc.cluster.local:9094',
  'avro-confluent.basic-auth.credentials-source' = 'USER_INFO',
  'avro-confluent.basic-auth.user-info' = '@@SCHEMA_REGISTRY_USER_INFO@@'
);

INSERT INTO customers VALUES
  ('customer-1', 'customer-1', 'Ada Lovelace', 'silver.m3_customers'),
  ('customer-2', 'customer-2', 'Grace Hopper', 'silver.m3_customers');
