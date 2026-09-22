namespace argocd.applications.kafka_connect;

internal class KafkaConnect
{
    public KafkaConnect(Kubernetes.Provider provider, ArgoApplicationSettings settings)
    {
        var application = new ArgoApplicationBuilder("kafka-connect", provider, settings)
            .SyncWave(3);  // After Flink (wave 2) to ensure schemas exist; PreSync hook validates
        if (settings.IsolatedNamespaces)
        {
            application = application
                .InNamespace("lakehouse-kafka-connect")
                .CreateNamespace();
        }
        application.Build();
    }
}
