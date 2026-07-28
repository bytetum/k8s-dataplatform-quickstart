# ARM64 Kafka Connect image for the Mac-local Strimzi deployment.
#
# The base is deliberately the same Strimzi/Kafka release family used by the
# operator.  All connector artifacts are architecture-independent JVM files;
# Docker resolves the base and Confluent builder images for TARGETPLATFORM.
ARG STRIMZI_VERSION=0.47.0
ARG KAFKA_VERSION=4.0.0
ARG CONFLUENT_VERSION=7.9.0
ARG ICEBERG_SOURCE_COMMIT=4a432839233f2343a9eae8255532f911f06358ef

FROM confluentinc/cp-kafka-connect:${CONFLUENT_VERSION} AS confluent-libs

FROM eclipse-temurin:17-jdk AS iceberg-builder

ARG ICEBERG_SOURCE_COMMIT
WORKDIR /work

# Iceberg does not publish this Connect runtime as a Maven Central distribution.
# Build the release source pinned to the commit behind apache-iceberg-1.7.1.
RUN apt-get update && \
    apt-get install --yes --no-install-recommends git && \
    rm -rf /var/lib/apt/lists/*

RUN git init . && \
    git remote add origin https://github.com/apache/iceberg.git && \
    git fetch --depth 1 origin "${ICEBERG_SOURCE_COMMIT}" && \
    git checkout --detach FETCH_HEAD && \
    test "$(git rev-parse HEAD)" = "${ICEBERG_SOURCE_COMMIT}" && \
    printf '1.7.1\n' > /work/version.txt && \
    ./gradlew --no-daemon :iceberg-kafka-connect:iceberg-kafka-connect-runtime:distZip

FROM quay.io/strimzi/kafka:${STRIMZI_VERSION}-kafka-${KAFKA_VERSION}

ARG DEBEZIUM_VERSION=3.0.0.Final

USER root

# Keep each plugin in its own directory.  Strimzi's default plugin path
# includes /opt/kafka/plugins, and separate directories avoid dependency
# collisions during Connect plugin discovery.
RUN mkdir -p /opt/kafka/plugins/debezium-postgres \
             /opt/kafka/plugins/iceberg \
             /opt/kafka/plugins/confluent-avro

RUN curl --fail --location --silent --show-error \
      --output /tmp/debezium-postgres.tar.gz \
      "https://repo.maven.apache.org/maven2/io/debezium/debezium-connector-postgres/${DEBEZIUM_VERSION}/debezium-connector-postgres-${DEBEZIUM_VERSION}-plugin.tar.gz" && \
    tar --extract --gzip --file /tmp/debezium-postgres.tar.gz \
      --directory /opt/kafka/plugins/debezium-postgres && \
    rm -f /tmp/debezium-postgres.tar.gz

COPY --from=iceberg-builder /work/kafka-connect/kafka-connect-runtime/build/distributions/iceberg-kafka-connect-runtime-*.zip /tmp/iceberg-runtime.zip

RUN unzip -q /tmp/iceberg-runtime.zip -d /opt/kafka/plugins/iceberg && \
    rm -f /tmp/iceberg-runtime.zip

# The Avro converter and Schema Registry client are copied as a complete
# dependency set from Confluent's ARM64-capable Connect image.  No registry
# endpoint, username, password, or other credential is baked into this image.
# Confluent packages the converter and its transitive runtime dependencies in
# kafka-serde-tools in the 7.9 image (there is no standalone converter folder).
COPY --from=confluent-libs /usr/share/java/kafka-serde-tools/ /opt/kafka/plugins/confluent-avro/

RUN chown -R 1001:0 /opt/kafka/plugins && \
    chmod -R g=u /opt/kafka/plugins

USER 1001
