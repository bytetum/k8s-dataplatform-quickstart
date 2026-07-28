# Mac-local Kind bootstrap runbook

This runbook creates and bootstraps a new Kind cluster named
`dataplatform-mac`, whose kubectl context is `kind-dataplatform-mac`. It is a
clean rebuild, not a migration of Kubernetes state.

The scripts in `scripts/mac` fail closed. Kubernetes-facing operations require
the current context to match `kind-dataplatform-mac`. Manifest rendering is
clusterless by construction. The infrastructure preview/bootstrap helpers
additionally replace `KUBECONFIG` for Pulumi with a temporary file containing
only that Kind cluster. They do not delete, recreate, or operate on the existing
`kind` cluster, the legacy WSL cluster, or `aks-emissary-test`.

## Choose a laptop profile

Kind shares the CPU and memory assigned to Docker Desktop; adding Kind workers
does not add capacity. Configure Docker Desktop before creating the cluster.

| Profile | Docker Desktop minimum | Intended scope |
| --- | --- | --- |
| `foundation` | 4 CPUs, 8 GiB RAM | Kind, Argo CD, cert-manager, External Secrets |
| `operators` | 4 CPUs, 8 GiB RAM | Foundation plus Strimzi and Flink operators |
| `core` | 8 CPUs, 16 GiB RAM | Foundation plus a staged subset of core data services |
| `full` | 12 CPUs, 24 GiB RAM | Best-effort full stack; enable only after staged validation |

The `full` profile is still likely to need workload-specific tuning. Kafka
Connect and Trino alone request substantial resources, and the repository also
defines Flink, OpenMetadata, databases, operators, WarpStream, Polaris, and
Marquez. Prefer `core`; enable Applications in stages and leave CPU/RAM
headroom between stages.

The preflight gate enforces these minimums:

```bash
./scripts/mac/preflight.sh --profile core --for-create
```

## 1. Prerequisites and safety checks

Install Docker Desktop, native Kind, kubectl, Pulumi, and the .NET SDK. On Apple
Silicon, `kind version` must report `darwin/arm64`; translated `darwin/amd64`
builds are rejected.

Inspect local identities without changing them:

```bash
uname -m
kind version
kind get clusters
kubectl config current-context
kubectl config get-contexts -o name
docker info --format 'CPUs={{.NCPU}} memory-bytes={{.MemTotal}} arch={{.Architecture}}'
```

It is normal for the current context to be AKS before cluster creation.
Do not run any unguarded `kubectl apply`, Helm, or infrastructure Pulumi command
from that context.

## 2. Create the dedicated cluster

`Files/kind-mac.yaml` binds the API server to `127.0.0.1` and lets Kind choose a
free host port. The helper accepts only the fixed cluster name
`dataplatform-mac` and refuses to continue if that cluster already exists.

Preview the exact command:

```bash
./scripts/mac/create-cluster.sh --profile core --dry-run
```

Create with the default Kubernetes v1.35.5 node image, pinned by digest from
the [Kind v0.32.0 release](https://github.com/kubernetes-sigs/kind/releases/tag/v0.32.0):

```bash
./scripts/mac/create-cluster.sh --profile core
```

To approve a different tested Kubernetes node image explicitly:

```bash
./scripts/mac/create-cluster.sh \
  --profile core \
  --image kindest/node:v1.35.5@sha256:ce977ae6d65918d0b58a5f8b5e940429c2ce42fa3a5619ec2bbc60b949c0ac95
```

The helper does not contain a delete or recreate path. A failed or pre-existing
cluster requires diagnosis and an explicit owner decision; do not repurpose the
legacy cluster.

Verify the guarded context:

```bash
./scripts/mac/preflight.sh --profile core
kubectl --context kind-dataplatform-mac get nodes -o wide
```

## 3. Prepare local Pulumi stacks

Pulumi state from WSL is not present in this clone. Use separate local stacks;
do not import or overwrite the old environment's state. Confirm the backend
with `pulumi whoami -v` and use the repository's approved secrets provider.

Manifest rendering uses the `argocd` and `applications` projects. The Mac-local
Argo Application configuration is `gitops/argocd/Pulumi.mac-local.yaml`. Select
the stacks, or initialize them once if this Mac backend has no state:

```bash
(cd gitops/argocd && pulumi stack select mac-local) ||
  (cd gitops/argocd && pulumi stack init mac-local)

(cd gitops/applications && pulumi stack select mac-local) ||
  (cd gitops/applications && pulumi stack init mac-local)
```

Create a distinct infrastructure stack:

```bash
cd infrastructure
pulumi stack init mac-local
pulumi config set kube_context kind-dataplatform-mac
cd ..
```

Do not put credentials on command lines or in tracked Pulumi YAML. Supply
required secrets only through the approved local secret workflow. Never use
`--show-secrets`, paste secret values into terminal output, or reuse placeholder
credentials as if they were real. The Mac helpers read the stack passphrase from
the macOS Keychain service `k8s-dataplatform-quickstart-pulumi`; they never echo
it.

## 4. Render and review GitOps manifests

Rendering intentionally rewrites generated files, so it requires an explicit
`--yes`. Mac Argo Application YAML is isolated under
`gitops/environments/mac/manifests/argocd`; the helper validates that exact path
and clears it before every profile render so stale Applications cannot survive a
profile downgrade. Workload manifests remain under `gitops/manifests`. The
helper still requires the safe context, while Pulumi's render providers remain
deliberately clusterless.

```bash
./scripts/mac/render-manifests.sh --project all --yes
git status --short
git diff -- gitops/manifests
```

Before bootstrap, confirm that the reviewed manifests:

- use the intended HTTPS or approved private repository authentication;
- follow the reviewed `mac-local` revision rather than an accidental `HEAD`;
- enable only the selected staged profile;
- contain no plaintext or placeholder credentials presented as real values;
- reference durable multi-architecture images for enabled workloads.

Rendering does not push Git. Argo CD cannot reconcile local-only files, so the
reviewed revision must be committed and pushed by the repository owner before
bootstrap.

## 5. Preview and bootstrap the foundation

The preview is non-mutating and must succeed with the expected context:

```bash
./scripts/mac/preview-bootstrap.sh --stack mac-local
```

Read the full diff. Stop if it targets any cluster other than
`kind-dataplatform-mac`, contains unexpected deletions, exposes credentials, or
would enable an unreviewed profile.

Apply only after the preview, Git revision, repository access, secret plumbing,
and enabled `foundation` profile have been reviewed:

```bash
./scripts/mac/bootstrap.sh --stack mac-local --yes
```

`bootstrap.sh` runs another preview immediately before apply and repeats the
context guard. It installs infrastructure only through the `infrastructure`
Pulumi project; workload reconciliation remains controlled by the reviewed
Argo CD Applications and Git revision.

Wait for the foundation to create the `local-secrets` namespace and install the
External Secrets resources. Keep later profiles disabled until their seed
Secrets are ready.

## 6. Seed secrets without exposing values

Prepare the ignored local input directly on the Mac:

```bash
cp .local-secrets/mac.env.example .local-secrets/mac.env
chmod 600 .local-secrets/mac.env
# Also chmod 600 each exact PEM path referenced by the env file.
```

Fill only the entries required by the profile you are about to enable, and the
referenced PEM files, without sending values through chat or shell output. Then
run the corresponding seed command:

```bash
./scripts/mac/seed-secrets.sh --profile core
```

`core` seeds only the source Secrets used by WarpStream, WarpStream Schema
Registry, Polaris/Postgres, and Iceberg. It does not require or seed the Flink,
Kafka Connect, registry, or Pricefiles database entries. `full` seeds core and
adds the Flink and Kafka Connect requirements:

```bash
./scripts/mac/seed-secrets.sh --profile full
```

Use `./scripts/mac/seed-secrets.sh --help` for the complete profile summary.
The `foundation` and `operators` profiles do not have workload source Secrets
to seed.

The seed helper refuses a context other than `kind-dataplatform-mac`, requires
private file permissions, and writes only source Secrets in `local-secrets`.
Verify only ExternalSecret status and key names, never decoded values. Do not
enable dependent profiles until all of their ExternalSecrets are ready.

## 7. Validate in stages

Always pass the context explicitly in ad hoc read-only checks:

```bash
kubectl --context kind-dataplatform-mac get nodes
kubectl --context kind-dataplatform-mac get namespaces
kubectl --context kind-dataplatform-mac get pods -A
kubectl --context kind-dataplatform-mac get applications -n argocd
kubectl --context kind-dataplatform-mac get externalsecrets -A
```

Recommended gates are:

1. Foundation: Argo CD, cert-manager, External Secrets, and the secret store are
   healthy.
2. Core: enable WarpStream/Strimzi and Polaris/PostgreSQL one service at a time.
3. Query/lineage: add Trino, then Marquez.
4. Processing/integration: add Flink and Kafka Connect only after durable arm64
   images and every external dependency are verified.
5. Heavy metadata: enable OpenMetadata last.

Check Docker Desktop headroom before every gate. `Synced` is not sufficient:
require healthy Applications, ready ExternalSecrets, no pending/crash-looping
pods, and a profile-specific end-to-end smoke test.

For loopback-only Argo CD access:

```bash
kubectl --context kind-dataplatform-mac \
  -n argocd port-forward service/argocd-server 8080:443
```

## Stop conditions

Stop immediately if a guard reports a context other than
`kind-dataplatform-mac`, the dedicated cluster already exists unexpectedly, a
preview names AKS or the legacy cluster, secrets appear in output, or an enabled
image is amd64-only/expired. Diagnose without changing the old clusters; their
continued availability is the rollback path until the Mac profile passes its
acceptance tests.
