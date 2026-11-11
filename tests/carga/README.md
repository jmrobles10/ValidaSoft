# Pruebas de carga (k6)

`certificacion.k6.js` mide `GET /api/v1/consultas/certificacion`, la consulta que hace Marco antes de pagar o contratar
([docs/08 §2.1](../../docs/08-integracion-y-migracion.md), [docs/04 §12](../../docs/04-arquitectura-plataforma.md)).

**Objetivo (H9): p99 < 100 ms a 500 rps** con la mezcla que produce un ERP con caché: la mayor parte de las consultas lleva
`If-None-Match` y recibe **304**; las demás, sin copia, reciben **200**; unas pocas son de terceros que no existen (200 `SinVinculo`).
El objetivo de H11 es p99 < 50 ms (docs/09, ADR-0007).

## Correrla

k6 no se instala en el repositorio: se usa la imagen oficial **con versión exacta**.

```bash
# 1. Una API local con la base de datos que quieras (ver «Entorno local» abajo), p. ej. en http://localhost:5115
# 2. k6 en Docker; host.docker.internal es la máquina que corre Docker Desktop
MSYS_NO_PATHCONV=1 docker run --rm -i -v "$PWD/tests/carga:/scripts" -w /scripts \
  -e API_URL=http://host.docker.internal:5115 \
  -e PLATAFORMA_KEY=dev-plataforma-solo-local-cambiar-0001 \
  grafana/k6:1.0.0 run certificacion.k6.js
```

Con k6 instalado: `API_URL=… PLATAFORMA_KEY=… k6 run tests/carga/certificacion.k6.js`.

`setup()` siembra un grupo de desarrollo —empresa, flujo de EMPLEADO con un área automática, una API key solo con `consultas:leer` y
150 terceros certificados, sin enviar correos— y las 500 consultas por segundo salen de ahí durante un minuto. La corrida deja
`resultado-carga.json` (no se versiona) y termina con código 99 si algún umbral falla.

### Contra un entorno ya sembrado

```bash
API_URL=https://valida.staging.example API_KEY=vsk_live_… EMPRESA_ID=<guid> NITS=900000001,900000002 k6 run certificacion.k6.js
```

### Variables

| Variable | Por omisión | Qué hace |
|---|---|---|
| `API_URL` | `http://localhost:5080` | Origen de la API |
| `PLATAFORMA_KEY` | — | Clave de arranque de Plataforma: siembra un grupo de desarrollo |
| `API_KEY`, `EMPRESA_ID`, `NITS` | — | Entorno ya sembrado (en lugar de `PLATAFORMA_KEY`) |
| `RPS` / `DURACION` | `500` / `60s` | Tasa de llegada y duración |
| `TERCEROS` | `150` | Terceros certificados que siembra |
| `P99_MS` | `100` | Umbral del p99 |
| `PORCENTAJE_CON_ETAG` | `80` | De las consultas con copia, cuántas mandan `If-None-Match` |
| `PORCENTAJE_DESCONOCIDOS` | `2` | Consultas de terceros inexistentes |

## Entorno local

La medida vale para el binario que se mide: publica en `Release` y levanta la API con una base propia (no la de desarrollo):

```bash
docker compose -f infra/compose/compose.yaml up -d postgres minio
docker exec validasoft-postgres-1 createdb -U validasoft validasoft_carga
dotnet publish src/Validasoft.Api -c Release -o /tmp/api-carga
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__Validasoft="Host=localhost;Port=5432;Database=validasoft_carga;Username=validasoft;Password=validasoft" \
  dotnet /tmp/api-carga/Validasoft.Api.dll --urls http://localhost:5115
```

Los números dependen de la máquina y de lo que más corra en ella (PostgreSQL local, otros procesos): compáralos entre corridas de la
misma máquina y repite si hubo ruido. El resultado de cada medición va en [docs/11](../../docs/11-trazabilidad-por-hito.md) (H9).
