using Pulumi.Kubernetes.ApiExtensions;

namespace Pulumi.Crds.Flink
{
    public class FlinkDeployment : Kubernetes.ApiExtensions.CustomResource
    {
        [Output("spec")]
        public Output<FlinkDeploymentSpec> Spec { get; private set; } = null!;

        public FlinkDeployment(string name, FlinkDeploymentArgs args, CustomResourceOptions? options = null)
            : base(name, args, options)
        {
        }
    }

    [OutputType]
    public sealed class FlinkDeploymentSpec
    {
        [Output("flinkConfiguration")]
        public Output<FlinkConfigurationSpec> FlinkConfiguration { get; private set; } = null!;

        [Output("flinkVersion")]
        public Output<string> FlinkVersion { get; private set; } = null!;

        [Output("image")]
        public Output<string> Image { get; private set; } = null!;

        [Output("job")]
        public Output<JobSpec> Job { get; private set; } = null!;

        [Output("jobManager")]
        public Output<JobManagerSpec> JobManager { get; private set; } = null!;

        [Output("podTemplate")]
        public Output<PodTemplateSpec> PodTemplate { get; private set; } = null!;

        [Output("service")]
        public Output<ServiceSpec> Service { get; private set; } = null!;

        [Output("serviceAccount")]
        public Output<string> ServiceAccount { get; private set; } = null!;

        [Output("taskManager")]
        public Output<TaskManagerSpec> TaskManager { get; private set; } = null!;
    }

    public class FlinkDeploymentArgs : CustomResourceArgs
    {
        [Input("spec")]
        public Input<FlinkDeploymentSpecArgs>? Spec { get; set; }

        public FlinkDeploymentArgs()
            : base("flink.apache.org/v1beta1", "FlinkDeployment")
        {
        }
    }

    public class FlinkDeploymentSpecArgs : ResourceArgs
    {
        [Input("flinkConfiguration")]
        public Input<FlinkConfigurationSpecArgs>? FlinkConfiguration { get; set; }

        [Input("flinkVersion")]
        public Input<string>? FlinkVersion { get; set; }

        [Input("image")]
        public Input<string>? Image { get; set; }

        [Input("job")]
        public Input<JobSpecArgs>? Job { get; set; }

        [Input("jobManager")]
        public Input<JobManagerSpecArgs>? JobManager { get; set; }

        [Input("podTemplate")]
        public Input<PodTemplateSpecArgs>? PodTemplate { get; set; }

        [Input("service")]
        public Input<ServiceSpecArgs>? Service { get; set; }

        [Input("serviceAccount")]
        public Input<string>? ServiceAccount { get; set; }

        [Input("taskManager")]
        public Input<TaskManagerSpecArgs>? TaskManager { get; set; }
    }

    [OutputType]
    public sealed class JobSpec
    {
        [Output("args")]
        public Output<List<string>> Args { get; private set; } = null!;

        [Output("entryClass")]
        public Output<string> EntryClass { get; private set; } = null!;

        [Output("jarURI")]
        public Output<string> JarURI { get; private set; } = null!;

        [Output("parallelism")]
        public Output<int> Parallelism { get; private set; } = null!;

        [Output("upgradeMode")]
        public Output<string> UpgradeMode { get; private set; } = null!;
    }

    public class JobSpecArgs : ResourceArgs
    {
        [Input("args")]
        public InputList<string>? Args { get; set; }

        [Input("entryClass")]
        public Input<string>? EntryClass { get; set; }

        [Input("jarURI")]
        public Input<string>? JarURI { get; set; }

        [Input("parallelism")]
        public Input<int>? Parallelism { get; set; }

        [Input("upgradeMode")]
        public Input<string>? UpgradeMode { get; set; }
    }

    [OutputType]
    public sealed class JobManagerSpec
    {
        [Output("resource")]
        public Output<ResourceSpec> Resource { get; private set; } = null!;
    }

    public class JobManagerSpecArgs : ResourceArgs
    {
        [Input("resource")]
        public Input<ResourceSpecArgs>? Resource { get; set; }
    }

    [OutputType]
    public sealed class TaskManagerSpec
    {
        [Output("resource")]
        public Output<ResourceSpec> Resource { get; private set; } = null!;
        
        [Output("serviceAccount")]
        public Output<string> ServiceAccount { get; private set; } = null!;
    }

    public class TaskManagerSpecArgs : ResourceArgs
    {
        [Input("resource")]
        public Input<ResourceSpecArgs>? Resource { get; set; }
        
        [Input("serviceAccount")]
        public Input<string>? ServiceAccount { get; set; }
    }

    [OutputType]
    public sealed class ResourceSpec
    {
        [Output("cpu")]
        public Output<double> Cpu { get; private set; } = null!;

        [Output("memory")]
        public Output<string> Memory { get; private set; } = null!;
    }

    public class ResourceSpecArgs : ResourceArgs
    {
        [Input("cpu")]
        public Input<double>? Cpu { get; set; }

        [Input("memory")]
        public Input<string>? Memory { get; set; }
    }

    [OutputType]
    public sealed class PodTemplateSpec
    {
        [Output("spec")]
        public Output<Pulumi.Kubernetes.Types.Outputs.Core.V1.PodSpec> Spec { get; private set; } = null!;
        
        [Output("serviceAccount")]
        public Output<string> ServiceAccount { get; private set; } = null!;
    }

    public class PodTemplateSpecArgs : ResourceArgs
    {
        [Input("spec")]
        public Input<Pulumi.Kubernetes.Types.Inputs.Core.V1.PodSpecArgs>? Spec { get; set; }
        
        [Input("serviceAccount")]
        public Input<string>? ServiceAccount { get; set; }
    }

    [OutputType]
    public sealed class ServiceSpec
    {
        [Output("port")]
        public Output<int> Port { get; private set; } = null!;

        [Output("targetPort")]
        public Output<int> TargetPort { get; private set; } = null!;

        [Output("type")]
        public Output<string> Type { get; private set; } = null!;
    }

    public class ServiceSpecArgs : ResourceArgs
    {
        [Input("port")]
        public Input<int>? Port { get; set; }

        [Input("targetPort")]
        public Input<int>? TargetPort { get; set; }

        [Input("type")]
        public Input<string>? Type { get; set; }
    }
    
    [OutputType]
    public sealed class FlinkConfigurationSpec
    {
        [Output("taskmanager.numberOfTaskSlots")]
        public Output<string> TaskManagerNumberOfTaskSlots { get; private set; } = null!;

        [Output("state.savepoints.dir")]
        public Output<string> StateSavepointsDir { get; private set; } = null!;

        [Output("state.checkpoints.dir")]
        public Output<string> StateCheckpointsDir { get; private set; } = null!;

        [Output("high-availability")]
        public Output<string> HighAvailability { get; private set; } = null!;

        [Output("high-availability.storageDir")]
        public Output<string> HighAvailabilityStorageDir { get; private set; } = null!;

        [Output("jobmanager.archive.fs.dir")]
        public Output<string> JobManagerArchiveFsDir { get; private set; } = null!;

        [Output("jobstore.dir")]
        public Output<string> JobStoreDir { get; private set; } = null!;

        [Output("jobmanager.scheduler")]
        public Output<string> JobManagerScheduler { get; private set; } = null!;

        [Output("env.java.opts")]
        public Output<string> EnvJavaOpts { get; private set; } = null!;
        
        [Output("kafka.bootstrap.servers")]
        public Output<string> KafkaBootstrapServers { get; private set; } = null!;

        [Output("kafka.input.topic")]
        public Output<string> KafkaInputTopic { get; private set; } = null!;

        [Output("kafka.output.topic")]
        public Output<string> KafkaOutputTopic { get; private set; } = null!;

        [Output("state.backend")]
        public Output<string> StateBackend { get; private set; } = null!;

        [Output("state.backend.incremental")]
        public Output<string> StateBackendIncremental { get; private set; } = null!;

        [Output("state.backend.rocksdb.localdir")]
        public Output<string> StateBackendRocksDbLocalDir { get; private set; } = null!;
    }

    public class FlinkConfigurationSpecArgs : ResourceArgs
    {
        [Input("sql-gateway.endpoint.type")]
        public Input<string>? SqlGateWayType { get; set; }
        
        [Input("taskmanager.numberOfTaskSlots")]
        public Input<string>? TaskManagerNumberOfTaskSlots { get; set; }

        [Input("state.savepoints.dir")]
        public Input<string>? StateSavepointsDir { get; set; }

        [Input("state.checkpoints.dir")]
        public Input<string>? StateCheckpointsDir { get; set; }

        [Input("high-availability")]
        public Input<string>? HighAvailability { get; set; }

        [Input("high-availability.storageDir")]
        public Input<string>? HighAvailabilityStorageDir { get; set; }

        [Input("jobmanager.archive.fs.dir")]
        public Input<string>? JobManagerArchiveFsDir { get; set; }

        [Input("jobstore.dir")]
        public Input<string>? JobStoreDir { get; set; }

        [Input("jobmanager.scheduler")]
        public Input<string>? JobManagerScheduler { get; set; }

        [Input("env.java.opts")]
        public Input<string>? EnvJavaOpts { get; set; }
        
        [Input("kafka.bootstrap.servers")]
        public Input<string>? KafkaBootstrapServers { get; set; }

        [Input("kafka.input.topic")]
        public Input<string>? KafkaInputTopic { get; set; }

        [Input("kafka.output.topic")]
        public Input<string>? KafkaOutputTopic { get; set; }

        [Input("state.backend")]
        public Input<string>? StateBackend { get; set; }

        [Input("state.backend.incremental")]
        public Input<string>? StateBackendIncremental { get; set; }

        [Input("state.backend.rocksdb.localdir")]
        public Input<string>? StateBackendRocksDbLocalDir { get; set; }
    }
    
}