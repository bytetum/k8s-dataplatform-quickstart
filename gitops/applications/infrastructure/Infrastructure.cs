namespace applications.infrastructure;

internal class Infrastructure : ComponentResource
{
    public Infrastructure(string manifestsRoot, bool renderCertManager = true)
        : base("manifests", "infrastructure")
    {
        if (renderCertManager)
        {
            _ = new CertManager(manifestsRoot);
        }
        _ = new Secrets(manifestsRoot);

        // TODO: add infrastructure applications, that are shared across namespaces
        // (external-secrets, monitoring, etc.)
    }
}