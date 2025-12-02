using Pulumi.Kubernetes.ApiExtensions;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;

namespace Pulumi.Crds.FlinkDeployment
{
    public class FlinkDeployment : Kubernetes.ApiExtensions.CustomResource
    {
        [Output("spec")] public Output<FlinkDeploymentSpec> Spec { get; private set; } = null!;

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

        [Output("flinkVersion")] public Output<string> FlinkVersion { get; private set; } = null!;

        [Output("image")] public Output<string> Image { get; private set; } = null!;

        [Output("imagePullPolicy")] public Output<string> ImagePullPolicy { get; private set; } = null!;

        [Output("job")] public Output<JobSpec> Job { get; private set; } = null!;

        [Output("jobManager")] public Output<JobManagerSpec> JobManager { get; private set; } = null!;

        [Output("podTemplate")] public Output<PodTemplateSpec> PodTemplate { get; private set; } = null!;

        [Output("service")] public Output<ServiceSpec> Service { get; private set; } = null!;

        [Output("serviceAccount")] public Output<string> ServiceAccount { get; private set; } = null!;

        [Output("taskManager")] public Output<TaskManagerSpec> TaskManager { get; private set; } = null!;
    }

    public class FlinkDeploymentArgs : CustomResourceArgs
    {
        [Input("spec")] public Input<FlinkDeploymentSpecArgs>? Spec { get; set; }

        public FlinkDeploymentArgs()
            : base("flink.apache.org/v1beta1", "FlinkDeployment")
        {
        }
    }

    public class FlinkDeploymentSpecArgs : ResourceArgs
    {
        [Input("flinkConfiguration")] public Input<FlinkConfigurationSpecArgs>? FlinkConfiguration { get; set; }

        [Input("flinkVersion")] public Input<string>? FlinkVersion { get; set; }

        [Input("image")] public Input<string>? Image { get; set; }

        [Input("imagePullPolicy")] public Input<string>? ImagePullPolicy { get; set; }

        [Input("job")] public Input<JobSpecArgs>? Job { get; set; }

        [Input("jobManager")] public Input<JobManagerSpecArgs>? JobManager { get; set; }

        [Input("podTemplate")] public Input<PodTemplateSpecArgs>? PodTemplate { get; set; }

        [Input("service")] public Input<ServiceSpecArgs>? Service { get; set; }

        [Input("serviceAccount")] public Input<string>? ServiceAccount { get; set; }

        [Input("taskManager")] public Input<TaskManagerSpecArgs>? TaskManager { get; set; }
    }

    [OutputType]
    public sealed class JobSpec
    {
        [Output("args")] public Output<List<string>> Args { get; private set; } = null!;

        [Output("entryClass")] public Output<string> EntryClass { get; private set; } = null!;

        [Output("jarURI")] public Output<string> JarURI { get; private set; } = null!;

        [Output("parallelism")] public Output<int> Parallelism { get; private set; } = null!;

        [Output("upgradeMode")] public Output<string> UpgradeMode { get; private set; } = null!;
    }

    public class JobSpecArgs : ResourceArgs
    {
        [Input("args")] public InputList<string>? Args { get; set; }

        [Input("entryClass")] public Input<string>? EntryClass { get; set; }

        [Input("jarURI")] public Input<string>? JarURI { get; set; }

        [Input("parallelism")] public Input<int>? Parallelism { get; set; }

        [Input("upgradeMode")] public Input<string>? UpgradeMode { get; set; }
    }

    [OutputType]
    public sealed class JobManagerSpec
    {
        [Output("resource")] public Output<ResourceSpec> Resource { get; private set; } = null!;
    }

    public class JobManagerSpecArgs : ResourceArgs
    {
        [Input("resource")] public Input<ResourceSpecArgs>? Resource { get; set; }
    }

    [OutputType]
    public sealed class TaskManagerSpec
    {
        [Output("resource")] public Output<ResourceSpec> Resource { get; private set; } = null!;

        [Output("serviceAccount")] public Output<string> ServiceAccount { get; private set; } = null!;
    }

    public class TaskManagerSpecArgs : ResourceArgs
    {
        [Input("resource")] public Input<ResourceSpecArgs>? Resource { get; set; }

        [Input("serviceAccount")] public Input<string>? ServiceAccount { get; set; }
    }

    [OutputType]
    public sealed class ResourceSpec
    {
        [Output("cpu")] public Output<double> Cpu { get; private set; } = null!;

        [Output("memory")] public Output<string> Memory { get; private set; } = null!;
    }

    public class ResourceSpecArgs : ResourceArgs
    {
        [Input("cpu")] public Input<double>? Cpu { get; set; }

        [Input("memory")] public Input<string>? Memory { get; set; }
    }

    [OutputType]
    public sealed class PodTemplateSpec
    {
        [Output("spec")]
        public Output<Pulumi.Kubernetes.Types.Outputs.Core.V1.PodSpec> Spec { get; private set; } = null!;

        [Output("serviceAccount")] public Output<string> ServiceAccount { get; private set; } = null!;
    }

    [OutputType]
    public sealed class ServiceSpec
    {
        [Output("port")] public Output<int> Port { get; private set; } = null!;

        [Output("targetPort")] public Output<int> TargetPort { get; private set; } = null!;

        [Output("type")] public Output<string> Type { get; private set; } = null!;
    }

    public class ServiceSpecArgs : ResourceArgs
    {
        [Input("port")] public Input<int>? Port { get; set; }

        [Input("targetPort")] public Input<int>? TargetPort { get; set; }

        [Input("type")] public Input<string>? Type { get; set; }
    }

    [OutputType]
    public sealed class FlinkConfigurationSpec
    {
        [Output("taskmanager.numberOfTaskSlots")]
        public Output<string> TaskManagerNumberOfTaskSlots { get; private set; } = null!;

        [Output("high-availability.type")] public Output<string> HighAvailabilityType { get; private set; } = null!;

        [Output("high-availability.storageDir")]
        public Output<string> HighAvailabilityStorageDir { get; private set; } = null!;

        [Output("execution.checkpointing.interval")]
        public Output<string> ExecutionCheckpointingInterval { get; private set; } = null!;

        [Output("execution.checkpointing.storage")]
        public Output<string> ExecutionCheckpointingStorage { get; private set; } = null!;

        [Output("execution.checkpointing.dir")]
        public Output<string> ExecutionCheckpointingDir { get; private set; } = null!;

        [Output("execution.checkpointing.savepoint-dir")]
        public Output<string> ExecutionCheckpointingSavepointDir { get; private set; } = null!;

        [Output("state.backend.type")] public Output<string> StateBackendType { get; private set; } = null!;

        [Output("execution.checkpointing.incremental")]
        public Output<bool> ExecutionCheckpointingIncremental { get; private set; } = null!;

        [Output("state.backend.rocksdb.localdir")]
        public Output<string> StateBackendRocksDbLocalDir { get; private set; } = null!;

        [Output("jobmanager.archive.fs.dir")]
        public Output<string> JobManagerArchiveFsDir { get; private set; } = null!;

        [Output("kafka.bootstrap.servers")] public Output<string> KafkaBootstrapServers { get; private set; } = null!;
    }

    public class FlinkConfigurationSpecArgs : ResourceArgs
    {
        [Input("taskmanager.numberOfTaskSlots")]
        public Input<string>? TaskManagerNumberOfTaskSlots { get; set; }

        [Input("high-availability.type")] public Input<string>? HighAvailabilityType { get; set; }

        [Input("high-availability.storageDir")]
        public Input<string>? HighAvailabilityStorageDir { get; set; }

        [Input("execution.checkpointing.interval")]
        public Input<string>? ExecutionCheckpointingInterval { get; set; }

        [Input("execution.checkpointing.storage")]
        public Input<string>? ExecutionCheckpointingStorage { get; set; }

        [Input("execution.checkpointing.dir")] public Input<string>? ExecutionCheckpointingDir { get; set; }

        [Input("execution.checkpointing.savepoint-dir")]
        public Input<string>? ExecutionCheckpointingSavepointDir { get; set; }

        [Input("state.backend.type")] public Input<string>? StateBackendType { get; set; }

        [Input("execution.checkpointing.incremental")]
        public Input<bool>? ExecutionCheckpointingIncremental { get; set; }

        [Input("state.backend.rocksdb.localdir")]
        public Input<string>? StateBackendRocksDbLocalDir { get; set; }

        [Input("jobmanager.archive.fs.dir")] public Input<string>? JobManagerArchiveFsDir { get; set; }

        [Input("kafka.bootstrap.servers")] public Input<string>? KafkaBootstrapServers { get; set; }
    }
}