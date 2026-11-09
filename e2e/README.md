# Pruebas de punta a punta

Playwright con un navegador real contra Validasoft tal como se despliega: la API sirve la app embebible en `/app/` y la del tercero en `/`, con PostgreSQL, Mailpit (correo) y MinIO (archivos) de verdad. Cada prueba crea su propio grupo empresarial, así que corren en paralelo sin pisarse ni depender de datos previos.

```bash
docker compose -f infra/compose/compose.yaml up -d postgres mailpit minio
pnpm --filter @validasoft/e2e navegadores   # la primera vez: descarga Chromium
pnpm e2e                                     # compila las apps, publica la API en e2e/.api y la levanta en :5090
```

Con una API ya levantada que sirva las dos apps: `E2E_BASE_URL=http://localhost:5090 pnpm e2e`. Para no recompilar: `E2E_SIN_COMPILAR=1`.

## Cómo se arma cada caso

- **Empresa** (`soporte/semilla.ts`): grupo con Constructora Norte (Compras y Cumplimiento; Cumplimiento puede rechazar) y Constructora Sur (Revisión), el sistema integrado E2E con sus claves y personas con roles distintos.
- **Proveedor** (`soporte/proveedor.ts`): entra con el enlace del correo y el código (Mailpit), acepta, sube documentos (PDF generados en `soporte/archivos.ts`) y responde observaciones.
- **Personas de la empresa** (`soporte/embebido.ts`, `soporte/empresa.ts`): usan Validasoft embebido en un iframe de otro origen; la sesión llega por `postMessage` con identidad delegada firmada por el sistema integrado.
- **Sistema integrado** (`soporte/receptor.ts`): recibe los webhooks y verifica su firma.

## Qué cubre

| Archivo | Casos |
|---|---|
| `decisiones.spec.ts` | Aprobación de las dos áreas; observación con versión corregida que invalida lo decidido; rechazo; rechazo durante la devolución; respuesta con explicación; aprobación en nombre de un área |
| `multiempresa.spec.ts` | Dos empresas del grupo con el mismo proveedor; proveedor que también es cliente; dos grupos distintos con su propia vigencia |
| `actualizaciones.spec.ts` | Versión nueva durante la revisión; decisión con la pantalla vieja; formato no admitido y archivo repetido; actualización desde la bóveda después de aprobado |
| `vencimientos.spec.ts` | Vigente, por vencer, en gracia y vencido (con avisos); fecha futura; recálculo al cambiar la regla |
| `certificado.spec.ts` | Verificación pública auténtica y PDF; huella o folio inválidos; bloqueo y desbloqueo; revocación |
| `eventos.spec.ts` | Webhooks firmados en orden; entrega fallida y reenvío; campana y correo a la revisora |

Los vencimientos se prueban sin mover el reloj: el proveedor declara una fecha de expedición vieja y los avisos o vencimientos que caen en el pasado se ejecutan enseguida.
