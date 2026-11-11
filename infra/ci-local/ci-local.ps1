# Replica local del CI (ci.yml) en contenedores Linux. Entrada para PowerShell; el trabajo lo hace ci-local.mjs.
#   .\infra\ci-local\ci-local.ps1                      # backend, frontend y e2e sobre esta carpeta
#   .\infra\ci-local\ci-local.ps1 --solo frontend      # un trabajo
#   .\infra\ci-local\ci-local.ps1 --rama origin/main   # lo confirmado en una rama, sin tocar ninguna carpeta
#   .\infra\ci-local\ci-local.ps1 --ayuda
# (Solo ASCII en este archivo: PowerShell 5.1 lee los .ps1 sin BOM como ANSI.)
$ErrorActionPreference = 'Stop'

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('ci-local: falta Node.js (>= 22) en el PATH.')
    exit 2
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('ci-local: falta Docker (abre Docker Desktop).')
    exit 2
}

# La salida lleva tildes: que la consola las muestre bien.
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

& node (Join-Path $PSScriptRoot 'ci-local.mjs') @args
exit $LASTEXITCODE
