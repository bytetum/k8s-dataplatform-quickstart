namespace argocd.applications.kafka_connect;

internal class KafkaConnect
{
    public KafkaConnect(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("kafka-connect", provider)
            .SyncWave(3)  // After Flink (wave 2) to ensure schemas exist; PreSync hook validates
            .Build();
    }
}