# Plan: remap the WSL-hosted GitOps setup to this Mac

Status: **Foundation implemented on 2026-07-26 — core and later gates await local secret inputs and ARM64 image validation**

## Outcome

Create a new, reproducible local data-platform environment on this Apple Silicon
Mac, managed by its own Argo CD installation and reconciled from this repository.
Keep the old WSL cluster available as a rollback source until the Mac environment
passes the agreed validation checks.

The recommended target is **Docker Desktop + a separately named Kind cluster**.
That is the lowest-change path because the repo already contains a Kind config and
the Mac already has Docker, Kind, kubectl, Helm, Pulumi, and .NET installed.

## What the repository and Mac currently tell us

- The Mac is `arm64`; the installed Kind executable reports a Darwin `amd64`
  build, so it is likely running through translation and should be replaced with
  a native build before relying on it.
- Docker Desktop is installed, but its engine currently reports no usable CPUs or
  memory. No Kind cluster is present.
- `kubectl` currently selects `aks-emissary-test`, not a local cluster. All
  bootstrap commands therefore need an explicit context guard.
- Pulumi uses the local `file://~` backend on this Mac, and there are no local
  infrastructure stacks yet. The WSL Pulumi state did not move with the Git clone.
- `Files/kind-config.txt` exposes the Kubernetes API on `0.0.0.0:6443`. A
  Mac-local cluster should bind to loopback and should not assume port 6443 is free.
- Argo CD is bootstrapped from `infrastructure/gitops/ArgoCD.cs`. It installs an
  app-of-apps that reads `gitops/manifests/argocd`.
- The bootstrap code and generated Applications hard-code
  `git@github.com:bytetum/k8s-dataplatform-quickstart.git` and default to `HEAD`.
  The Mac clone itself uses the HTTPS origin.
- The committed `Infrastructure:argo_secret_key` value and Redis secret are
  placeholders/hard-coded values. The generated `ClusterSecretStore` also uses a
  fake provider populated with placeholders. These must not be treated as migrated
  credentials.
- The workload is not architecture-neutral as committed:
  - Kafka Connect uses `ttl.sh/hxt-kafka-connect-amd64-20-12:24h`, an
    amd64-specific, short-lived image.
  - Flink uses the local-only `flink-test:2.1.1` image with
    `imagePullPolicy: Never`.
  - This repo contains neither image's Dockerfile/build recipe.
- The full stack is heavy for a laptop: Kafka Connect alone requests 2 CPU/4 GiB;
  Trino requests another 1 CPU/4 GiB across coordinator and worker; the enabled
  Flink deployments/session cluster, OpenMetadata dependencies, operators,
  databases, WarpStream, Polaris, and Marquez add substantially more.
- Several services depend on resources outside Kind: S3-compatible buckets,
  WarpStream credentials, a source PostgreSQL database, and possibly container
  registries. Moving Kubernetes does not move those systems or their data.

## Decisions to approve before implementation

1. **Migration mode:** use a clean local Kubernetes rebuild. AWS/S3, WarpStream,
   and the other cloud services remain in place; only their access from the new
   cluster is re-established. If in-cluster PostgreSQL/OpenMetadata/Marquez data
   must survive, add explicit database backup and restore work before cutover.
2. **Git revision isolation:** create a `mac-local` configuration branch (or an
   equivalent reviewed branch) and make the new Argo CD instance follow it during
   migration. Change to the normal branch only after acceptance.
3. **Repository authentication:** if the GitHub repository is public, use the
   HTTPS URL and remove the unnecessary SSH credential objects. If private, create
   a new read-only GitHub deploy key for the Mac Argo CD instance and store its
   private half outside Git.
4. **Local secret source:** the cloud credentials will be supplied by the owner.
   The recommended handoff is a git-ignored local input file with restrictive
   permissions, used once to create an out-of-band seed Secret in a dedicated
   namespace and read through External Secrets' Kubernetes provider. A
   password-manager/cloud-secret-store integration is also acceptable. Do not
   paste secrets into chat, commit actual values, or reuse the fake provider.
5. **Workload profile:** start with a `core` profile and enable the full data stack
   in stages. Treat `full` as optional unless Docker Desktop can dedicate enough
   resources.
6. **Image strategy:** build/publish durable multi-architecture images. Temporary
   amd64 emulation may be used only as a diagnostic fallback, not as the final
   setup.

## Execution plan

### 1. Capture the old WSL environment

Do this while the old machine and cluster remain available.

- Record the Git commit/revision followed by Argo CD and export the Argo
  Application list with sync/health status.
- Record Kubernetes version, namespaces, CRDs, storage classes, PVCs/PVs, ingress
  and service exposure, and the images actually running.
- Inventory the non-secret names/keys required by every ExternalSecret. Record
  where each real value comes from; do not place decoded Secret data in this repo.
- Locate the missing build sources for `flink-test:2.1.1` and the custom Kafka
  Connect image, including plugins and exact versions.
- Decide which in-cluster databases contain data worth keeping. For each one,
  make and test a logical backup. AWS/S3 and WarpStream data stay in their current
  cloud services; record their endpoints, regions, non-secret IDs, and access
  requirements without copying their data.
- Save the old cluster's Argo and workload status as migration evidence, not as
  manifests to reapply over the Git-managed resources.

Acceptance: the old environment can be recreated conceptually from Git plus the
documented secret/image/data inputs, and every stateful dataset has an explicit
keep-or-discard decision.

### 2. Prepare and guard the Mac runtime

- Start Docker Desktop and verify the Linux engine is healthy.
- Allocate resources based on the chosen profile. Begin with at least 8 CPU and
  16 GiB for a reduced profile; use approximately 12 CPU/24 GiB or more for a
  serious attempt at the full stack, subject to the Mac's actual capacity.
- Install/use a native `arm64` Kind binary.
- Replace the current Kind config with a version-controlled Mac-local config:
  use a dedicated cluster name such as `dataplatform-mac`, bind the API server to
  `127.0.0.1`, and avoid forcing host port 6443.
- Create the cluster with an explicit Kubernetes version, then verify that the
  resulting context is `kind-dataplatform-mac`.
- Add a preflight script/task that refuses to run if the current context is not
  the expected local context. Infrastructure commands should also set the
  Pulumi Kubernetes provider context explicitly.

Acceptance: Docker reports the allocated resources; the Kind node is `Ready`;
`kubectl --context kind-dataplatform-mac get nodes` works; and no command has
touched `aks-emissary-test` or the WSL cluster.

### 3. Make environment identity explicit in code

- Add a local environment/profile configuration for:
  - cluster context/name;
  - Git repository URL and target revision;
  - enabled Argo Applications;
  - resource sizing;
  - external endpoints, bucket names/regions, and non-secret IDs.
- Replace the hard-coded repo URL in both `infrastructure/gitops/ArgoCD.cs` and
  `gitops/argocd/applications/ArgoApplication.cs`.
- Make the bootstrap Application's `targetRevision` an actual configurable Pulumi
  input property and pin it to the migration revision rather than implicit `HEAD`.
- Keep generated YAML under `gitops/manifests` derived from the Pulumi source; do
  not hand-edit generated manifests.
- Add a short Mac bootstrap runbook and repeatable commands (Make/Task/script)
  rather than relying on terminal history.

Acceptance: a source search finds no environment-specific Git URL or accidental
`HEAD` default in the bootstrap path, and the generated Application manifests all
point at the reviewed Mac migration revision.

### 4. Replace credentials and seed local secrets safely

- Remove the committed placeholder Redis/repository credential behavior. Let the
  chart generate or consume a newly created secret as appropriate.
- Implement the approved local secret backend and create an ignored example file
  containing names only.
- Scaffold a path such as `.local-secrets/mac.env`, add the real file to
  `.gitignore`, provide `.local-secrets/mac.env.example` with empty values, and
  restrict the real file to the local user. The owner fills the real file directly
  on the Mac; secret values are never sent through chat or echoed in command
  output.
- Seed every value required by the existing ExternalSecrets, including S3/object
  storage, WarpStream agent/schema registry, Polaris, registry, and source database
  credentials.
- Where the Git repository is private, create a distinct read-only deploy key for
  this Argo installation. Where public, use HTTPS with no repository secret.
- Verify ExternalSecret `Ready=True` without printing secret values.

Acceptance: no real secret appears in Git, chat, shell history, Pulumi preview
output, or the runbook; Argo can read the repo; and all enabled ExternalSecrets
become ready.

### 5. Make custom images work on Apple Silicon

- Recover or recreate the missing Docker build definitions from the old
  environment.
- Build `linux/arm64` images, preferably a durable `linux/amd64,linux/arm64`
  manifest published under immutable version/digest tags.
- Replace the expiring `ttl.sh/...:24h` Kafka Connect reference with the durable
  image and verify all required connector plugins are present.
- Build or pull the Flink image, then either publish it or load it into the named
  Kind cluster. If keeping `imagePullPolicy: Never`, make `kind load docker-image
  --name dataplatform-mac ...` an explicit bootstrap step.
- Scan every enabled image for an arm64 manifest before enabling its Application.

Acceptance: no enabled workload references an amd64-only or expired image; no pod
shows `exec format error`, `ErrImageNeverPull`, or `ImagePullBackOff`.

### 6. Introduce staged laptop profiles

Generate only the Applications enabled by the selected profile.

- **Foundation:** cert-manager, Argo CD, External Secrets, secret store.
- **Core data services:** WarpStream agent/schema registry, Strimzi operator,
  Polaris/PostgreSQL.
- **Query/lineage:** Trino, then Marquez.
- **Processing:** Flink operator, session mode, selected Flink deployments.
- **Integration:** Kafka Connect only after its source DB, schemas, custom image,
  registry access, and object storage are verified.
- **Heavy metadata:** OpenMetadata dependencies and OpenMetadata last.

Add readiness gates between waves; a sync wave orders creation but does not prove
that an external dependency or database is usable. Tune requests/limits per
profile instead of silently overcommitting the Docker VM.

Acceptance: each stage is `Synced` and `Healthy`, its smoke test passes, and the
Docker VM retains enough memory/CPU headroom before the next stage is enabled.

### 7. Regenerate and review GitOps artifacts

- Create new local Pulumi stacks for manifest generation and infrastructure;
  do not assume the absent WSL state can be imported from the clone.
- Use a new secrets passphrase/provider and explicit Mac stack names. Never copy a
  state file casually between machines.
- Run .NET build and Pulumi previews first.
- Regenerate `gitops/manifests/argocd` and workload manifests from source.
- Review the diff for deletions, namespace changes, repo/revision drift, unpinned
  images, plaintext credentials, and resource changes.
- Commit and push the reviewed manifests to the migration revision because Argo
  cannot reconcile unpushed files from this Mac checkout.

Acceptance: builds/previews succeed, generated-manifest diffs contain only
intentional changes, secret scanning is clean, and the migration revision is
available to Argo.

### 8. Bootstrap Argo CD into the Mac cluster

- Select/create the Mac infrastructure stack and set its Kubernetes context
  explicitly to `kind-dataplatform-mac`.
- Preview, then apply the Argo CD installation.
- Access the UI/API through a loopback `kubectl port-forward`; do not expose it on
  all interfaces by default.
- Confirm repo access and app-of-apps revision before allowing automated sync.
- Enable automated sync one profile/stage at a time. Keep pruning disabled during
  initial observation if the reviewed diff is not yet fully trusted; restore the
  desired prune/self-heal policy after validation.

Acceptance: the Mac Argo CD instance is reachable locally, follows only the
intended migration revision, and manages only the Mac cluster.

### 9. Validate behavior, not only pod status

- Platform: all enabled Applications `Synced/Healthy`; no pending pods, failed
  hooks, crash loops, architecture errors, or missing CRDs.
- Secrets: all enabled ExternalSecrets are ready and workloads can authenticate
  without exposing credential values.
- Storage: use an explicitly approved test prefix to create/read/delete a test
  object through the retained AWS/S3 service, and confirm the expected
  bucket/region. Do not test destructive operations against an unscoped production
  prefix.
- WarpStream/schema registry: produce and consume a test record and register/read
  a test schema.
- Polaris/Trino: create a disposable namespace/table and query it; also run a TPCH
  smoke query.
- Flink: submit one selected job, verify checkpoints/state behavior, and confirm
  its local image and S3 SQL/JAR assets are available.
- Kafka Connect: verify plugin inventory and connector health, then test with a
  disposable source/table before enabling real CDC.
- Lineage/metadata: confirm a test job appears in Marquez/OpenMetadata if those
  profiles are enabled.
- Restart one Kind node/Docker Desktop only if persistence across local restarts is
  an explicit requirement; document the expected recovery behavior.

Acceptance: the chosen profile passes its end-to-end tests and the results are
recorded against the exact Git commit and image digests.

### 10. Cut over and retain rollback

- Freeze changes during the final comparison.
- If the old WSL cluster is still running, disable its Argo automated sync before
  moving the normal branch forward, so both clusters do not reconcile the same
  mutable revision unexpectedly.
- Promote the reviewed Mac configuration/revision, then observe at least one full
  reconcile and workload test cycle.
- Keep database backups, old context details, and the WSL cluster until the agreed
  retention window expires.
- Only then remove old Argo credentials and decommission the WSL cluster. Treat
  cluster deletion and data deletion as separate, explicitly approved actions.

Rollback: disable sync on the Mac, restore the prior Git revision, and resume the
old WSL Argo instance. If data was written during the Mac test, reconcile that data
separately before switching writers.

## Primary files expected to change

- `Files/kind-config.txt` (or replace it with `Files/kind-mac.yaml`)
- `infrastructure/Infrastructure.cs`
- `infrastructure/gitops/ArgoCD.cs`
- a new Mac infrastructure Pulumi stack config
- `gitops/argocd/applications/ArgoApplication.cs`
- `gitops/argocd/applications/ArgoApplications.cs`
- a new Mac manifest-generation stack/profile config
- `gitops/applications/Program.cs` and builders that carry image/resources
- generated content under `gitops/manifests`
- a new bootstrap/preflight script or task plus a concise runbook
- new Docker build definitions, unless they are maintained in a separate,
  explicitly documented image repository

## Stop conditions

Pause rather than continuing if any of these occur:

- the active Kubernetes context is not `kind-dataplatform-mac`;
- Docker lacks the resources required by the selected profile;
- the Git revision has unreviewed changes or is not pushed;
- an actual secret would need to be committed or printed;
- a required custom image/build recipe cannot be recovered;
- an Application points at the old cluster, wrong repo, or wrong branch;
- a stateful workload needs data but no tested backup exists;
- enabling the next stage would exceed available Mac resources.
