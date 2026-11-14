---
titulo: "Despliegue y operación"
seccion: "Arquitectura"
orden: 17
resumen: "La imagen Docker, el compose local con sus perfiles, el arranque de la base, la salud, las réplicas, qué respaldar y qué verifica el CI."
---

Validasoft se despliega como **una imagen** más sus tres dependencias: PostgreSQL, un almacén S3 y un servidor SMTP. Debe poder correr completo en un portátil y en un servidor del cliente (decisión de `docs/04`: Docker Compose como contrato de despliegue). Todas las variables están en [Variables de entorno](guia:variables-de-entorno).

## La imagen

`src/Validasoft.Api/Dockerfile` se construye con el repositorio como contexto y tiene tres etapas:

1. **Frontend:** Node 26 con `pnpm` (por `corepack`) instala con `--frozen-lockfile` y compila `apps/web` y `apps/public`.
2. **API:** el SDK de .NET 10 publica `Validasoft.Api` en Release.
3. **Ejecución:** `mcr.microsoft.com/dotnet/aspnet:10.0` con la API y las dos apps compiladas en `/app/frontend/web` y `/app/frontend/public`. Corre como un usuario sin privilegios y escucha en el puerto **8080**.

Las apps se activan con `Validasoft__Apps__Web` y `Validasoft__Apps__Publica` (la imagen ya las fija); vacías, la API no las sirve. Con ello **todo es un solo origen**: la API, la app de la empresa en `/app/` y el portal del tercero en `/`.

## Entorno local con Docker Compose

`infra/compose/compose.yaml` define los servicios de apoyo y, opcionalmente, la API:

| Comando | Qué levanta |
|---|---|
| `docker compose -f infra/compose/compose.yaml up -d` | PostgreSQL 17 (5432), Mailpit (SMTP en 1025 y bandeja web en 8025) y MinIO (S3 en 9000 y consola en 9001). La API corre con `dotnet run`. |
| `docker compose -f infra/compose/compose.yaml --profile app up -d --build` | Lo anterior más la API en un contenedor (puerto 5080 del anfitrión), con claves de desarrollo en el compose. |
| Perfil `e2e` | Un segundo Mailpit (SMTP 1026 y web 8026) para que los correos de las pruebas de punta a punta no lleguen al de desarrollo; lo levanta el propio arranque de la prueba. |

Las claves, contraseñas y llaves que trae el compose y `appsettings.Development.json` son **solo locales** y están marcadas así. En desarrollo la API crea el esquema al arrancar (`CrearEsquemaAlIniciar`), crea el bucket de la bóveda (`CrearBucketsAlIniciar`) y siembra los catálogos de la ficha; hay documentación interactiva en `/docs`.

Día a día, desde la raíz del repositorio:

```bash
pnpm install
dotnet run --project src/Validasoft.Api      # API en http://localhost:5080 (según launchSettings)
pnpm dev                                     # app de la empresa (apps/web)
pnpm dev:public                              # portal del tercero (apps/public)
```

## Producción

- **Base de datos.** El esquema no se crea al arrancar (`CrearEsquemaAlIniciar` es `false` por omisión): se aplica antes con el comando de JasperFx, `dotnet Validasoft.Api.dll db-apply`. Así el arranque de la API no paga una migración ni compite con otras réplicas.
- **Claves obligatorias.** La API **no arranca** sin `Validasoft__Secretos__ClaveMaestra` y sin `Validasoft__Certificados__ClavePrivada`; sin `Validasoft__Sesiones__ClaveFirma` falla el inicio de sesión por token. Se guardan en un gestor de secretos, nunca en la imagen ni en el repositorio.
- **Primer grupo.** Con `Validasoft__Plataforma__ClaveBootstrap` se da de alta al primer operador de Plataforma; luego se puede desactivar la clave desde la consola. El primer grupo se crea desde `/plataforma/grupos/nuevo`.
- **Correo.** Un SMTP real (`Correo__Host`, puerto, seguridad, usuario y clave), con el remitente de la plataforma y la `UrlPublica` que se escribirá en los enlaces. Cada empresa puede registrar un dominio propio ([Dominio de correo](guia:configuracion-de-la-empresa#dominio-de-correo)).
- **Almacén.** Un S3 compatible con `Endpoint` y, si el navegador lo ve con otra dirección, `EndpointPublico`. La API crea el bucket `boveda` y, con la primera carga de un documento propio, el de cada grupo.
- **Passkeys.** `Validasoft__Ingreso__Passkeys__RpId` debe ser el dominio desde el que se sirve la app y `Origenes` la lista de sus orígenes.
- **HTTPS y proxy.** La API habla HTTP: el TLS lo termina un proxy delante. Hay que declarar los proxies en `Validasoft__Proxies__Confiables` para que `X-Forwarded-For` dé la IP real al límite por IP y a la auditoría, y mantener `CookieSegura` en `true`.
- **Proveedores externos.** Infolaft, Stradata, Compliance e IA están apagados por omisión: se encienden por configuración (`Proveedores:{nombre}:Habilitado`) y, para listas e IA, con las credenciales que carga Plataforma o cada empresa.

### Salud

| Ruta | Respuesta |
|---|---|
| `GET /health/live` | 200 `{ "estado": "vivo" }` mientras el proceso responda. |
| `GET /health/ready` | 200 `{ "estado": "listo" }` si abre una conexión a PostgreSQL; si no, 503 `sin-base-de-datos`. |
| `GET /openapi/v1.json` | El contrato OpenAPI 3.1 de la API. |

### Varias réplicas

El proceso admite más de una réplica del mismo servicio:

- El **daemon de eventos** corre en modo *hot/cold*: solo el nodo que gana el bloqueo consultivo de PostgreSQL procesa la suscripción de eventos públicos; si cae, otro toma el relevo.
- Las **cachés** de llaves, sistemas, políticas de ingreso, SSO y empresas se invalidan entre servidores con `LISTEN/NOTIFY` de PostgreSQL (`Validasoft__Invalidaciones__Activo`, activo por omisión); sin ese aviso, el límite es el de la propia caché (por ejemplo, 5 minutos para una llave revocada).
- Los **procesos periódicos** (barridos de plazos, SLA, recordatorios, vigencias, habeas data y planes; procesadores de certificados, verificaciones y revisión automática; limpiezas) corren solo en el **nodo líder**, elegido con otro bloqueo consultivo de PostgreSQL (`Validasoft__Lider__Habilitado`, activo por omisión; `__SegundosEspera`, cada cuánto reintenta una réplica que no es líder, 10). Si el líder cae, otra réplica lo releva en segundos. Las consultas de listas restrictivas que se facturan se reclaman con una clave por consulta programada, así que una verificación no se pide dos veces aunque dos pasadas se crucen.
- Las **colas** son locales y durables: sus mensajes pendientes sobreviven a un reinicio y los procesa el nodo que los reclama. Las de **webhooks** y de **IA** se particionan por tenant: dentro de un tenant van de a una y en orden, y un receptor caído o una carga masiva de un tenant no frena a los demás.
- El **límite por IP** se mantiene en la memoria de cada nodo; el conteo de llamadas a la API se acumula en memoria y se vuelca a la base cada 5 segundos.

### Qué respaldar

- **PostgreSQL** completo: es el event store, la configuración, el outbox y las sesiones.
- **El almacén S3**: la bóveda y los PDF de los certificados. El nombre de cada objeto es su SHA-256, así que un respaldo se puede verificar.
- **Las claves**: perder la clave maestra deja ilegibles los secretos cifrados (llaves de OpenAI, secretos de webhook, números de cuenta); perder la semilla del sello impide emitir y deja sin verificar los certificados nuevos (las claves públicas anteriores se publican con `Certificados__ClavesAnteriores`). Se rotan sin cortar el servicio: [Secretos](guia:configuracion-de-plataforma#secretos-y-clave-de-arranque).

## Qué verifica el CI

`.github/workflows/ci.yml` corre en cada push y pull request:

| Trabajo | Qué hace |
|---|---|
| Backend (.NET) | Restaura, compila `Validasoft.slnx` en Release **sin advertencias**, corre las pruebas de dominio y de integración (con Testcontainers: PostgreSQL y MinIO), empaqueta el SDK .NET y lista paquetes con vulnerabilidades. |
| Frontend (pnpm) | `pnpm install --frozen-lockfile`, `lint`, `typecheck`, `test` (Vitest), `build` y verifica el paquete npm del SDK. |
| Imagen Docker | Construye la imagen sin publicarla. |

Las pruebas de punta a punta (`.github/workflows/e2e.yml`) y los recorridos del manual **no** corren en cada push: se lanzan bajo demanda. `infra/ci-local` replica el CI en contenedores Linux en una máquina local (`infra/ci-local/ci-local.sh`). Los SDK se publican con `.github/workflows/sdk-publicar.yml`.
