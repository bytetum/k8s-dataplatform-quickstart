using infrastructure.Cluster;
using Pulumi;

namespace infrastructure;

internal class Infrastructure : Stack
{
    public Infrastructure()
        : base()
    {
        var certManager = new CertManager();
        var flink = new Flink(certManager);


    }
}

