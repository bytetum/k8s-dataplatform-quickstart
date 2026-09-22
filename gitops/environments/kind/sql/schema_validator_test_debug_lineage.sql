SET 'pipeline.name' = 'kind-local-customer-order-analytics';
SET 'table.exec.state.ttl' = '1 h';
SET 'table.exec.source.idle-timeout' = '5 s';

CREATE TABLE order_summary (
  record_key STRING,
  customer_id STRING,
  order_total DECIMAL(12, 2),
  iceberg_table STRING
) WITH (
  'connector' = 'kafka',
  'topic' = 'kind-local.debug.silver.m3.order_summary',
  'properties.bootstrap.servers' = 'warpstream-agent.lakehouse-warpstream.svc.cluster.local:9092',
  'properties.group.id' = 'kind-local-flink-lineage-orders',
  'scan.startup.mode' = 'earliest-offset',
  'format' = 'avro-confluent',
  'avro-confluent.url' = 'http://warpstream-schema-registry-warpstream-agent.lakehouse-warpstream.svc.cluster.local:9094',
  'avro-confluent.basic-auth.credentials-source' = 'USER_INFO',
  'avro-confluent.basic-auth.user-info' = '@@SCHEMA_REGISTRY_USER_INFO@@'
);

CREATE TABLE customers (
  record_key STRING,
  customer_id STRING,
  customer_name STRING,
  iceberg_table STRING
) WITH (
  'connector' = 'kafka',
  'topic' = 'kind-local.debug.silver.m3.customers',
  'properties.bootstrap.servers' = 'warpstream-agent.lakehouse-warpstream.svc.cluster.local:9092',
  'properties.group.id' = 'kind-local-flink-lineage-customers',
  'scan.startup.mode' = 'earliest-offset',
  'format' = 'avro-confluent',
  'avro-confluent.url' = 'http://warpstream-schema-registry-warpstream-agent.lakehouse-warpstream.svc.cluster.local:9094',
  'avro-confluent.basic-auth.credentials-source' = 'USER_INFO',
  'avro-confluent.basic-auth.user-info' = '@@SCHEMA_REGISTRY_USER_INFO@@'
);

CREATE TABLE customer_order_analytics (
  record_key STRING,
  customer_id STRING,
  customer_name STRING,
  order_total DECIMAL(12, 2),
  iceberg_table STRING
) WITH (
  'connector' = 'kafka',
  'topic' = 'kind-local.debug.silver.m3.customer_order_analytics',
  'properties.bootstrap.servers' = 'warpstream-agent.lakehouse-warpstream.svc.cluster.local:9092',
  'format' = 'avro-confluent',
  'avro-confluent.url' = 'http://warpstream-schema-registry-warpstream-agent.lakehouse-warpstream.svc.cluster.local:9094',
  'avro-confluent.basic-auth.credentials-source' = 'USER_INFO',
  'avro-confluent.basic-auth.user-info' = '@@SCHEMA_REGISTRY_USER_INFO@@'
);

INSERT INTO customer_order_analytics
SELECT
  orders.record_key,
  orders.customer_id,
  customers.customer_name,
  orders.order_total,
  'silver.m3_customer_order_analytics'
FROM order_summary AS orders
JOIN customers
  ON orders.customer_id = customers.customer_id;
