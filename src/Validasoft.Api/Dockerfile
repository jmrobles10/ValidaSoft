# Imagen única de Validasoft: la API sirve también las dos apps (SE-3, docs/04 §10) para que todo sea un solo origen.
#   docker build -f src/Validasoft.Api/Dockerfile .        (contexto = raíz del repositorio)
# Las apps quedan en /app/frontend y se activan con Validasoft__Apps__Web / Validasoft__Apps__Publica (por defecto
# encendidas en esta imagen; vacías = la API no las sirve).

# 1. Frontend: apps/web (embebible, en /app/) y apps/public (tercero, en /) con pnpm.
FROM node:26-bookworm-slim AS frontend
WORKDIR /repo
# Node 25+ ya no trae corepack: se instala explícito (con Node 22 también funciona) y usa la versión de pnpm de packageManager.
RUN npm install -g corepack@0.36.0 && corepack enable
# Primero los manifiestos: la capa de `pnpm install` se reutiliza mientras no cambien las dependencias.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/web/package.json apps/web/
COPY apps/public/package.json apps/public/
COPY packages/api-client/package.json packages/api-client/
COPY packages/ui/package.json packages/ui/
RUN pnpm install --frozen-lockfile
COPY tsconfig.base.json tsconfig.json ./
COPY packages/ packages/
COPY apps/ apps/
RUN pnpm --filter @validasoft/web --filter @validasoft/public build

# 2. API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props Validasoft.slnx ./
COPY src/ src/
RUN dotnet publish src/Validasoft.Api/Validasoft.Api.csproj -c Release -o /app

# 3. Ejecución.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=compilacion /app .
COPY --from=frontend /repo/apps/web/dist ./frontend/web
COPY --from=frontend /repo/apps/public/dist ./frontend/public
ENV Validasoft__Apps__Web=frontend/web \
    Validasoft__Apps__Publica=frontend/public
USER $APP_UID
ENTRYPOINT ["dotnet", "Validasoft.Api.dll"]
