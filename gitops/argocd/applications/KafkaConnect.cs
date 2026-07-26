namespace argocd.applications.kafka_connect;

internal class KafkaConnect
{
    public KafkaConnect(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        new ArgoApplicationBuilder("kafka-connect", provider, settings)
            .SyncWave(3)  // After Flink (wave 2) to ensure schemas exist; PreSync hook validates
            .Build();
    }
}
