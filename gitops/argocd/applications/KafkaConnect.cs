namespace argocd.applications.kafka_connect;

internal class KafkaConnect
{
    public KafkaConnect(Kubernetes.Provider provider)
    {
        new ArgoApplicationBuilder("kafka-connect", provider)
            .SyncWave(2)
            .Build();
    }
}