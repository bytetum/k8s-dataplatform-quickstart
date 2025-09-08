-- test_job.sql
-- A minimal Flink SQL script for testing the job submission client.

-- 1. Create a temporary source table using the 'datagen' connector.
--    This connector generates data in memory and requires no external system.
--    The job will automatically stop after producing 10 rows.
CREATE TABLE input_source (
                              id INT,
                              message VARCHAR
) WITH (
      'connector' = 'datagen',
      'number-of-rows' = '10'
      );

-- 2. Create a temporary sink table using the 'print' connector.
--    This connector simply prints the results to the TaskManager's standard output log.
CREATE TABLE output_sink (
                             id INT,
                             processed_message VARCHAR
) WITH (
      'connector' = 'print'
      );

-- 3. A simple INSERT...SELECT statement to move and transform the data.
--    This verifies that the SQL planner and job execution work.
INSERT INTO output_sink
SELECT
    id,
    'PROCESSED: ' || message
FROM
    input_source;