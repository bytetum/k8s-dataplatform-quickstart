CREATE TABLE readings (
  sensorId STRING,
  reading DOUBLE,
  eventTime_ltz AS TO_TIMESTAMP_LTZ(`timestamp`, 3),
  `ts` TIMESTAMP(3) METADATA FROM 'timestamp',
  `timestamp` BIGINT,
    WATERMARK FOR eventTime_ltz AS eventTime_ltz - INTERVAL '30' SECONDS
) WITH (
  'connector' = 'kafka',
  'topic' = 'sensor.readings',
  'properties.bootstrap.servers' = 'kafka:29092',
  'properties.group.id' = 'group.sensor.readings',
  'format' = 'json',
  'scan.startup.mode' = 'earliest-offset',
  'json.timestamp-format.standard' = 'ISO-8601',
  'json.fail-on-missing-field' = 'false',
  'json.ignore-parse-errors' = 'true'
);