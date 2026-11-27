#!/usr/bin/env bash
# The kind cluster the speckle-server of docker-compose-internal.yml launches its
# Kubernetes Jobs into (viewer-.dat generation). Used by CI and local runs alike.
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)

CLUSTER=${KIND_CLUSTER_NAME:-speckle-sdk-integration}
NODE_CONTAINER="${CLUSTER}-control-plane"
# The default node image of kind v0.31.0, the version CI installs.
NODE_IMAGE=kindest/node:v1.35.0@sha256:452d707d4862f52530247495d180205e029056831160e22870e37e3f6c1ac31f
NAMESPACE=conversion
NODE_POOL=conversion
CONVERTER_IMAGE=${SPECKLE_CONVERTER_IMAGE:-ghcr.io/specklesystems/speckle-converters:latest}
# Mounted into the server container by docker-compose-internal.yml.
KUBECONFIG_OUT="$ROOT/.kind/kubeconfig"
CHECK_IMAGE=registry.k8s.io/e2e-test-images/busybox:1.36.1-1

usage() {
  cat <<EOF
usage: $(basename "$0") <up|load-image|env|check|status|down>

  up          create cluster $CLUSTER (idempotent): one node labelled
              purpose=$NODE_POOL, namespace $NAMESPACE, internal kubeconfig at
              .kind/kubeconfig
  load-image  pull \$SPECKLE_CONVERTER_IMAGE if absent and load it into the node
  env         print the KEY=VALUE lines docker-compose-internal.yml interpolates
  check       prove a pod reaches the server (:3000) and garage (:9000) on the host
  status      print the Jobs, pods and events of namespace $NAMESPACE
  down        delete the cluster and its kubeconfig

KIND_CLUSTER_NAME overrides the cluster name ($CLUSTER).
EOF
}

log() { echo "$@" >&2; }

kc() { docker exec -i "$NODE_CONTAINER" kubectl --kubeconfig /etc/kubernetes/admin.conf "$@"; }

cluster_exists() { kind get clusters 2>/dev/null | grep -qx "$CLUSTER"; }

require_cluster() {
  cluster_exists || { log "cluster $CLUSTER does not exist; run: $0 up"; exit 1; }
}

# Pods and the host-side test client consume the same presigned S3 URLs, so they need
# one address of the host that both reach. Docker Desktop resolves host.docker.internal
# in every container; on Linux the gateway of the kind network is a host address that
# pods route to through the node.
host_address() {
  if docker exec "$NODE_CONTAINER" getent hosts host.docker.internal >/dev/null 2>&1; then
    echo host.docker.internal
    return
  fi
  docker network inspect kind \
    --format '{{range .IPAM.Config}}{{.Gateway}}{{"\n"}}{{end}}' | grep -m1 -E '^[0-9]+\.'
}

cmd_up() {
  mkdir -p "$(dirname "$KUBECONFIG_OUT")"
  if cluster_exists; then
    log "cluster $CLUSTER already exists"
  else
    # Never the developer's ~/.kube/config; the file is overwritten right below.
    kind create cluster --name "$CLUSTER" --image "$NODE_IMAGE" \
      --kubeconfig "$KUBECONFIG_OUT" --wait 120s --config - <<EOF
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes:
  - role: control-plane
    labels:
      purpose: $NODE_POOL
EOF
  fi
  kind get kubeconfig --name "$CLUSTER" --internal >"$KUBECONFIG_OUT"
  # The server image runs as a distroless non-root uid, not the file's owner.
  chmod 644 "$KUBECONFIG_OUT"
  kc create namespace "$NAMESPACE" --dry-run=client -o yaml | kc apply -f - >&2
  log "cluster $CLUSTER ready: namespace $NAMESPACE, node pool $NODE_POOL, kubeconfig $KUBECONFIG_OUT"
}

cmd_load_image() {
  require_cluster
  if ! docker image inspect "$CONVERTER_IMAGE" >/dev/null 2>&1; then
    log "pulling $CONVERTER_IMAGE (private GHCR package: docker login ghcr.io first)"
    docker pull --quiet "$CONVERTER_IMAGE" >&2
  fi
  kind load docker-image "$CONVERTER_IMAGE" --name "$CLUSTER" >&2
}

cmd_env() {
  require_cluster
  local host bind
  host=$(host_address)
  bind=$host
  # Docker Desktop forwards host.docker.internal to the host's loopback publishes.
  [[ $host == host.docker.internal ]] && bind=127.0.0.1
  echo "KIND_HOST_ADDRESS=$host"
  echo "KIND_PUBLISH_ADDRESS=$bind"
}

cmd_check() {
  require_cluster
  local host status=0
  host=$(host_address)
  for target in "server:3000" "garage:9000"; do
    local what=${target%%:*} port=${target##*:}
    if kc run "check-$what-$port" -n "$NAMESPACE" --rm -i --quiet --restart=Never \
      --image "$CHECK_IMAGE" -- nc -w 5 "$host" "$port" </dev/null >/dev/null 2>&1; then
      log "ok    a pod reaches $what at $host:$port"
    else
      log "FAIL  no pod reaches $what at $host:$port"
      status=1
    fi
  done
  if [[ $status -ne 0 && $host != host.docker.internal ]]; then
    local iface
    iface="br-$(docker network inspect kind --format '{{.Id}}' | cut -c1-12)"
    log "Is the compose stack up? A host firewall may also drop traffic from the kind"
    log "bridge ($iface); with ufw: sudo ufw allow in on $iface to any port 3000,9000 proto tcp"
  fi
  return $status
}

cmd_status() {
  require_cluster
  kc get jobs,pods -n "$NAMESPACE" -o wide
  kc get events -n "$NAMESPACE" --sort-by=.lastTimestamp
}

cmd_down() {
  kind delete cluster --name "$CLUSTER" --kubeconfig "$KUBECONFIG_OUT"
  rm -f "$KUBECONFIG_OUT"
}

case ${1:-} in
  up) cmd_up ;;
  load-image) cmd_load_image ;;
  env) cmd_env ;;
  check) cmd_check ;;
  status) cmd_status ;;
  down) cmd_down ;;
  *) usage; exit 1 ;;
esac
