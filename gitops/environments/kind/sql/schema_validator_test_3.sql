SET 'pipeline.name' = 'kind-local-product-inventory';
SET 'table.exec.source.idle-timeout' = '5 s';

CREATE TABLE bronze_csytab (
  ctstky STRING,
  ctstco STRING,
  description STRING,
  updated_at STRING
) WITH (
  'connector' = 'kafka',
  'topic' = 'kind-local.bronze.m3.csytab',
  'properties.bootstrap.servers' = 'warpstream-agent.lakehouse-warpstream.svc.cluster.local:9092',
  'properties.group.id' = 'kind-local-flink-product-inventory',
  'scan.startup.mode' = 'earliest-offset',
  'format' = 'avro-confluent',
  'avro-confluent.url' = 'http://warpstream-schema-registry-warpstream-agent.lakehouse-warpstream.svc.cluster.local:9094',
  'avro-confluent.basic-auth.credentials-source' = 'USER_INFO',
  'avro-confluent.basic-auth.user-info' = '@@SCHEMA_REGISTRY_USER_INFO@@'
);

CREATE TABLE product_inventory (
  record_key STRING,
  ctstky STRING,
  ctstco STRING,
  description STRING,
  updated_at STRING,
  iceberg_table STRING
) WITH (
  'connector' = 'kafka',
  'topic' = 'kind-local.debug.silver.m3.product_inventory',
  'properties.bootstrap.servers' = 'warpstream-agent.lakehouse-warpstream.svc.cluster.local:9092',
  'format' = 'avro-confluent',
  'avro-confluent.url' = 'http://warpstream-schema-registry-warpstream-agent.lakehouse-warpstream.svc.cluster.local:9094',
  'avro-confluent.basic-auth.credentials-source' = 'USER_INFO',
  'avro-confluent.basic-auth.user-info' = '@@SCHEMA_REGISTRY_USER_INFO@@'
);

INSERT INTO product_inventory
SELECT
  CONCAT(ctstco, ':', ctstky),
  ctstky,
  ctstco,
  description,
  updated_at,
  'silver.m3_product_inventory'
FROM bronze_csytab;
