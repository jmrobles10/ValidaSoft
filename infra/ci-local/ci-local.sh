#!/usr/bin/env bash
# Réplica local del CI (ci.yml) en contenedores Linux. Entrada para Git Bash; el trabajo lo hace ci-local.mjs.
#   infra/ci-local/ci-local.sh                      # backend, frontend y e2e sobre esta carpeta
#   infra/ci-local/ci-local.sh --solo frontend      # un trabajo
#   infra/ci-local/ci-local.sh --rama origin/main   # lo confirmado en una rama, sin tocar ninguna carpeta
#   infra/ci-local/ci-local.sh --ayuda
set -euo pipefail

aqui="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# En Git Bash (Windows) Node necesita la ruta con letra de unidad (C:/...), no /c/...
if aqui_nativo="$(cd "$aqui" && pwd -W 2>/dev/null)"; then aqui="$aqui_nativo"; fi

if ! command -v node >/dev/null 2>&1; then
  echo "ci-local: falta Node.js (>= 22) en el PATH." >&2
  exit 2
fi
if ! command -v docker >/dev/null 2>&1; then
  echo "ci-local: falta Docker (abre Docker Desktop)." >&2
  exit 2
fi

exec node "$aqui/ci-local.mjs" "$@"
