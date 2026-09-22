SET 'pipeline.name' = 'kind-local-order-seed';

CREATE TABLE order_summary (
  record_key STRING,
  customer_id STRING,
  order_total DECIMAL(12, 2),
  iceberg_table STRING
) WITH (
  'connector' = 'kafka',
  'topic' = 'kind-local.debug.silver.m3.order_summary',
  'properties.bootstrap.servers' = 'warpstream-agent.lakehouse-warpstream.svc.cluster.local:9092',
  'format' = 'avro-confluent',
  'avro-confluent.url' = 'http://warpstream-schema-registry-warpstream-agent.lakehouse-warpstream.svc.cluster.local:9094',
  'avro-confluent.basic-auth.credentials-source' = 'USER_INFO',
  'avro-confluent.basic-auth.user-info' = '@@SCHEMA_REGISTRY_USER_INFO@@'
);

INSERT INTO order_summary VALUES
  ('order-1001', 'customer-1', CAST(42.50 AS DECIMAL(12, 2)), 'silver.m3_order_summary'),
  ('order-1002', 'customer-2', CAST(19.95 AS DECIMAL(12, 2)), 'silver.m3_order_summary');
