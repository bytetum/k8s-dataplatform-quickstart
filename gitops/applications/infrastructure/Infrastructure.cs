namespace applications.infrastructure;

internal class Infrastructure : ComponentResource
{
    public Infrastructure(string manifestsRoot)
        : base("manifests", "infrastructure")
    {
        var certManager = new CertManager(manifestsRoot);
        var secrets = new Secrets(manifestsRoot);

        // TODO: add infrastructure applications, that are shared across namespaces
        // (external-secrets, monitoring, etc.)
    }
}