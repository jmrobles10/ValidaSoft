# @validasoft/sdk

Cliente TypeScript de la API v1 de [Validasoft](https://github.com/jmrobles10/ValidaSoft): la plataforma que valida a terceros
(proveedores, clientes, empleados…) para varias empresas. Pensado para el backend del SRM, de Marco, del Portal u otro ERP.

- **Sin dependencias**: `fetch` y Web Crypto. Node 20+ (o un navegador, solo para verificar webhooks: la API key nunca va en el navegador).
- **ESM y CommonJS**, con tipos.
- Vínculos, invitaciones, expedientes, certificados, **consulta de certificación** (con `ETag`), **eventos por pull**, **sandbox** y
  sesiones para pantallas embebidas.
- **Identidad delegada**: firma la aserción `X-Usuario-Delegado` de cada petición (RS256 o ES256, clave PKCS#8).
- **Webhooks**: verificación de la firma con tolerancia de 5 minutos y varios secretos durante una rotación.
- `Idempotency-Key` automática en todo POST (la misma en los reintentos); solo se reintenta lo idempotente.

La guía completa, de punta a punta, está en [docs/integracion.md](https://github.com/jmrobles10/ValidaSoft/blob/main/docs/integracion.md).

## Instalar

```bash
npm install @validasoft/sdk
# o: pnpm add @validasoft/sdk
```

## Configurar

```ts
import { ClienteValidasoft, crearFirmanteDeAserciones } from '@validasoft/sdk';

const validasoft = new ClienteValidasoft({
  urlBase: process.env.VALIDASOFT_URL!,
  apiKey: process.env.VALIDASOFT_API_KEY!, // vsk_live_… (o vsk_test_… en el sandbox); del gestor de secretos
  empresaId: process.env.VALIDASOFT_EMPRESA_ID, // X-Empresa
  // Solo si el sistema actúa en nombre de una persona (identidad delegada):
  firmante: crearFirmanteDeAserciones('SRM', process.env.VALIDASOFT_CLAVE_PRIVADA_PKCS8!),
});
```

```js
// CommonJS
const { ClienteValidasoft } = require('@validasoft/sdk');
```

## Invitar a un tercero

```ts
const vinculo = await validasoft.vinculos.crear(
  {
    tercero: {
      identificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456' },
      tipoPersona: 'Juridica',
      nombre: 'Aceros S.A.S.',
      contactos: [{ nombre: 'Ana Gómez', correo: 'ana@aceros.co', rol: 'Comercial' }],
    },
    tipoVinculo: 'PROVEEDOR',
    correoInvitacion: 'ana@aceros.co',
    referenciaExterna: { sistema: 'SRM', id: 'ADP-00123' },
  },
  'srm-sol-00123', // clave de idempotencia estable: un reintento no duplica
);
```

## Consultar la certificación antes de pagar

```ts
const r = await validasoft.consultas.certificacion({ tipoIdentificacion: 'NIT', numero: '900123456', tipoVinculo: 'PROVEEDOR' }, copia?.etag);
const datos = r.noModificado ? copia.datos : r.datos; // 304: sigue valiendo tu copia
if (datos?.bloqueado) {
  // no pagar: datos.motivoBloqueo dice por qué
}
```

## Recibir webhooks

```ts
import { validarWebhook, leerCabecera } from '@validasoft/sdk';

// Con el cuerpo CRUDO (sin parsear), como llegó.
const evento = await validarWebhook({
  secretos: [secretoActual, secretoAnterior], // el anterior solo durante una rotación
  firma: leerCabecera(req.headers, 'validasoft-signature'),
  cuerpo: cuerpoCrudo,
}); // lanza WebhookInvalidoError si la firma no coincide
```

## Probar en el sandbox

Una API key `vsk_test_…` opera en un entorno aislado: con la configuración de tu grupo, sin datos reales, con los correos retenidos
en un buzón y con los proveedores de IA y de listas simulados.

```ts
const sandbox = new ClienteValidasoft({ urlBase, apiKey: llaveDePrueba, empresaId: empresaDeSandbox });
const estado = await sandbox.sandbox.obtener(); // las empresas del sandbox y su id para X-Empresa
await sandbox.vinculos.crear(/* … */);
const correo = await sandbox.sandbox.esperarCorreo('ana@aceros.co'); // la invitación no salió: está en el buzón
const enlace = correo.enlaces[0]; // ábrelo para seguir el flujo como el tercero
await sandbox.sandbox.reiniciar(); // vuelve a empezar de cero
```

## Errores

Toda respuesta de error es un `ErrorValidasoft` con `estado` (HTTP), `codigo` (estable: decide con él, no con el texto), `detalle` y
`correlacion` (cítala al pedir soporte).

## Versionado

[Versionado semántico](https://semver.org/lang/es/) alineado con la API v1: las versiones menores y de parche no rompen nada; la mayor
llega con una API nueva. Los cambios están en [CHANGELOG.md](CHANGELOG.md).
