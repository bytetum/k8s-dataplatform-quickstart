This folder contains ArgoCD Applications, defined with Pulumi.

TODO: create the Applications for Flink, Warpstream, etc., see [initial ArgoCD setup](../../infrastructure/gitops/ArgoCD.cs) for Application creation.

Note: the Application only tells ArgoCD where to look for manifests, we also still need to create the pipelines for Pulumi. In order to create YAML instead of directly creating resources in K8s, the provider's KubeConfig should be an empty string and the RenderYamlToDirectory field should be set to the wanted path (likely `../manifests/`).

Even if the pipelines are not ready and you don't have access to the cluster, you can start working on this by verifying that it compiles properly and that the yaml it returns is valid, for instance by using a [playground](https://kodekloud.com/public-playgrounds).