# syntax=docker/dockerfile:1.7
# Native ARM64 Flink 2.1.1 image for the local kind cluster.
#
# The runner is intentionally a thin application JAR. Flink and connector
# libraries remain in their normal Flink lib/plugin locations so the same image
# can be used by the operator for application mode, session JM/TM, and SQL
# Gateway. Every downloaded runtime artifact is pinned by version and SHA-256.

ARG FLINK_VERSION=2.1.1
ARG FLINK_BASE_TAG=2.1.1-java17
# This is the linux/arm64 manifest digest (the multi-arch index is
# sha256:8afd108eef7e80926c2812d08735538550b8c8ae0f9e31d8c504819ead502478).
ARG FLINK_BASE_DIGEST=sha256:ec8c51bde88d4fefdfeca6c4386f090ae3fd397f5b3f08e0c6b0648e84cd3d49

FROM maven:3.9.11-eclipse-temurin-17@sha256:e4a7ace3dc0d645ed97f8d9ad0b0d3f0b14fa8d150138f27f116d7105a639b82 AS runner-builder

WORKDIR /build
COPY pom.xml ./
COPY src ./src

# Do not shade Flink into runner.jar: the Flink distribution owns these APIs.
RUN mvn --batch-mode --no-transfer-progress -DskipTests package && \
    test -s target/flink-sql-runner-2.1.1.jar

FROM apache/flink:${FLINK_BASE_TAG}@${FLINK_BASE_DIGEST}

ARG FLINK_VERSION
ARG KAFKA_CONNECTOR_VERSION=5.0.0-2.1
ARG KAFKA_CONNECTOR_SHA256=11cbbcd1163c4fae0d23f9e2c55b4ff57a719a39fe96fc780001cd21a4e5ba15
ARG AVRO_CONFLUENT_VERSION=2.1.1
ARG AVRO_CONFLUENT_SHA256=9e27e6da6d16d5a672f1771f5261899b753ed602eb9c52cc6cccf50d146f0868
ARG JSON_VERSION=2.1.1
ARG JSON_SHA256=40c386dd6bdcbf8f441a8ef12afa7201f8a0771b7ca0480d15beeea961560f51
ARG ICEBERG_VERSION=1.11.0
ARG ICEBERG_SHA256=39e658d876f253815b4b17c6676bebf6a24d517afe93a21298106d1d7fa97331
ARG ICEBERG_AWS_SHA256=38f01da7e96850cdd05e6616d758b77b43314b712a8808e3f9a824d56976162f
ARG HADOOP_VERSION=3.4.3
ARG HADOOP_CLIENT_API_SHA256=6443332781f26a7199009d9517cd1eb013fbf54ca1c9759a2a9e487542b1d52a
ARG HADOOP_CLIENT_RUNTIME_SHA256=bd20267b0555766ae84697dd888f074ca326d8e2ec3c0008928a6ac2966c67de
ARG S3_VERSION=2.1.1
ARG S3_SHA256=8ddf87a571122e402498d185936e3bf82f5eddd4438a91a279caff7942baa7b8
ARG OPENLINEAGE_VERSION=1.53.0
ARG OPENLINEAGE_SHA256=bd4daf546c3c6433002fd31e854f67d7b432a2e561fba0352b1d6c627e70f248

USER root

RUN set -eux; \
    mkdir -p /opt/flink/usrlib /opt/flink/plugins/s3-fs-hadoop; \
    curl --fail --location --silent --show-error \
      --output /opt/flink/lib/flink-sql-connector-kafka-${KAFKA_CONNECTOR_VERSION}.jar \
      https://repo.maven.apache.org/maven2/org/apache/flink/flink-sql-connector-kafka/${KAFKA_CONNECTOR_VERSION}/flink-sql-connector-kafka-${KAFKA_CONNECTOR_VERSION}.jar; \
    echo "${KAFKA_CONNECTOR_SHA256}  /opt/flink/lib/flink-sql-connector-kafka-${KAFKA_CONNECTOR_VERSION}.jar" | sha256sum --check --strict; \
    curl --fail --location --silent --show-error \
      --output /opt/flink/lib/flink-sql-avro-confluent-registry-${AVRO_CONFLUENT_VERSION}.jar \
      https://repo.maven.apache.org/maven2/org/apache/flink/flink-sql-avro-confluent-registry/${AVRO_CONFLUENT_VERSION}/flink-sql-avro-confluent-registry-${AVRO_CONFLUENT_VERSION}.jar; \
    echo "${AVRO_CONFLUENT_SHA256}  /opt/flink/lib/flink-sql-avro-confluent-registry-${AVRO_CONFLUENT_VERSION}.jar" | sha256sum --check --strict; \
    curl --fail --location --silent --show-error \
      --output /opt/flink/lib/flink-json-${JSON_VERSION}.jar \
      https://repo.maven.apache.org/maven2/org/apache/flink/flink-json/${JSON_VERSION}/flink-json-${JSON_VERSION}.jar; \
    echo "${JSON_SHA256}  /opt/flink/lib/flink-json-${JSON_VERSION}.jar" | sha256sum --check --strict; \
    curl --fail --location --silent --show-error \
      --output /opt/flink/lib/iceberg-flink-runtime-2.1-${ICEBERG_VERSION}.jar \
      https://repo.maven.apache.org/maven2/org/apache/iceberg/iceberg-flink-runtime-2.1/${ICEBERG_VERSION}/iceberg-flink-runtime-2.1-${ICEBERG_VERSION}.jar; \
    echo "${ICEBERG_SHA256}  /opt/flink/lib/iceberg-flink-runtime-2.1-${ICEBERG_VERSION}.jar" | sha256sum --check --strict; \
    curl --fail --location --silent --show-error \
      --output /opt/flink/lib/iceberg-aws-bundle-${ICEBERG_VERSION}.jar \
      https://repo.maven.apache.org/maven2/org/apache/iceberg/iceberg-aws-bundle/${ICEBERG_VERSION}/iceberg-aws-bundle-${ICEBERG_VERSION}.jar; \
    echo "${ICEBERG_AWS_SHA256}  /opt/flink/lib/iceberg-aws-bundle-${ICEBERG_VERSION}.jar" | sha256sum --check --strict; \
    curl --fail --location --silent --show-error \
      --output /opt/flink/lib/hadoop-client-api-${HADOOP_VERSION}.jar \
      https://repo.maven.apache.org/maven2/org/apache/hadoop/hadoop-client-api/${HADOOP_VERSION}/hadoop-client-api-${HADOOP_VERSION}.jar; \
    echo "${HADOOP_CLIENT_API_SHA256}  /opt/flink/lib/hadoop-client-api-${HADOOP_VERSION}.jar" | sha256sum --check --strict; \
    curl --fail --location --silent --show-error \
      --output /opt/flink/lib/hadoop-client-runtime-${HADOOP_VERSION}.jar \
      https://repo.maven.apache.org/maven2/org/apache/hadoop/hadoop-client-runtime/${HADOOP_VERSION}/hadoop-client-runtime-${HADOOP_VERSION}.jar; \
    echo "${HADOOP_CLIENT_RUNTIME_SHA256}  /opt/flink/lib/hadoop-client-runtime-${HADOOP_VERSION}.jar" | sha256sum --check --strict; \
    curl --fail --location --silent --show-error \
      --output /opt/flink/plugins/s3-fs-hadoop/flink-s3-fs-hadoop-${S3_VERSION}.jar \
      https://repo.maven.apache.org/maven2/org/apache/flink/flink-s3-fs-hadoop/${S3_VERSION}/flink-s3-fs-hadoop-${S3_VERSION}.jar; \
    echo "${S3_SHA256}  /opt/flink/plugins/s3-fs-hadoop/flink-s3-fs-hadoop-${S3_VERSION}.jar" | sha256sum --check --strict; \
    curl --fail --location --silent --show-error \
      --output /opt/flink/lib/openlineage-flink-${OPENLINEAGE_VERSION}.jar \
      https://repo.maven.apache.org/maven2/io/openlineage/openlineage-flink/${OPENLINEAGE_VERSION}/openlineage-flink-${OPENLINEAGE_VERSION}.jar; \
    echo "${OPENLINEAGE_SHA256}  /opt/flink/lib/openlineage-flink-${OPENLINEAGE_VERSION}.jar" | sha256sum --check --strict; \
    chown -R flink:flink /opt/flink/lib /opt/flink/plugins /opt/flink/usrlib

COPY --from=runner-builder --chown=flink:flink /build/target/flink-sql-runner-2.1.1.jar /opt/flink/usrlib/runner.jar

USER flink
