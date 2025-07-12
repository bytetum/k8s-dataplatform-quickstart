1. Delete and create cluster => pulumi refresh
2. manually add crds of cert-manager kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.18.2/cert-manager.crds.yaml
3. manually create namespace "ns-cert-manager" using lens
4. pulumi up certmanager
5. pulumi up flink (probably need 2 up)

#todo: find a way to delay flink deployment 