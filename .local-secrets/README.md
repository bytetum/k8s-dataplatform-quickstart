# Mac-local secret inputs

This directory is the local, out-of-band input to the Kubernetes-backed
External Secrets store. Git ignores every file here except this README and
`*.example` files.

1. Copy `mac.env.example` to `mac.env`.
2. Put the Polaris PEM files in this directory and set
   `POLARIS_PUBLIC_KEY_FILE` and `POLARIS_PRIVATE_KEY_FILE` to their paths.
   Relative paths are resolved from this directory.
3. Fill the values directly on the Mac. Values are literal text after the first
   `=`; do not add shell quotes.
4. Restrict the input files before using them:

   ```sh
   chmod 600 .local-secrets/mac.env .local-secrets/*.pem
   ```

5. After the `local-secrets` namespace and External Secrets resources have been
   rendered, reviewed, and applied through GitOps, run:

   ```sh
   scripts/mac/seed-secrets.sh
   ```

The seed script refuses to run outside `kind-dataplatform-mac`, validates local
file permissions, and never prints secret values. It creates or updates only
the source Secrets in the `local-secrets` namespace. Do not commit `mac.env`,
PEM files, generated Secret YAML, or decoded Secret data.
