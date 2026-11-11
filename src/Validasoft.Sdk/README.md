# Validasoft.Sdk

Cliente .NET de la API v1 de [Validasoft](https://github.com/jmrobles10/ValidaSoft): la plataforma que valida a terceros
(proveedores, clientes, empleados…) para varias empresas. Pensado para el SRM, Marco, el Portal u otro ERP.

- **Sin dependencias** fuera de la BCL. Multi-target `net7.0`, `net8.0` y `net10.0`.
- Vínculos, invitaciones, expedientes, certificados, **consulta de certificación** (con `ETag`), **eventos por pull**, **sandbox** y
  sesiones para pantallas embebidas.
- **Identidad delegada**: firma la aserción `X-Usuario-Delegado` de cada petición (RS256 o ES256).
- **Webhooks**: verificación de la firma con tolerancia de 5 minutos y varios secretos durante una rotación.
- `Idempotency-Key` automática en todo POST (la misma en los reintentos); solo se reintenta lo idempotente.

La guía completa, de punta a punta, está en [docs/integracion.md](https://github.com/jmrobles10/ValidaSoft/blob/main/docs/integracion.md).

## Instalar

```bash
dotnet add package Validasoft.Sdk
```

Trae `Validasoft.Contratos` (los DTO de la API) como dependencia.

## Configurar

```csharp
// Program.cs: un cliente tipado por IHttpClientFactory.
builder.Services.AddSingleton(new OpcionesValidasoft
{
    UrlBase = new Uri(builder.Configuration["Validasoft:UrlBase"]!),
    ApiKey = builder.Configuration["Validasoft:ApiKey"]!,            // vsk_live_… (o vsk_test_… en el sandbox); del gestor de secretos
    EmpresaId = Guid.Parse(builder.Configuration["Validasoft:EmpresaId"]!),
    // Solo si el sistema actúa en nombre de una persona (identidad delegada, ADR-0011):
    Firmante = new FirmanteDeAsercionesDelegadas("SRM", File.ReadAllText(builder.Configuration["Validasoft:ClavePrivadaPemArchivo"]!))
});
builder.Services.AddHttpClient<IClienteValidasoft, ClienteValidasoft>(http => http.Timeout = TimeSpan.FromSeconds(5));
```

La API key es secreta: va en el backend y en el gestor de secretos, nunca en el navegador ni en el repositorio.

## Invitar a un tercero

```csharp
var vinculo = await validasoft.Vinculos.CrearAsync(new CrearVinculoDto(
    TerceroId: null,
    Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", "900123456"), "Juridica", "Aceros S.A.S.", null,
        [new ContactoDto("Ana Gómez", "ana@aceros.co", null, "Comercial")]),
    TipoVinculo: "PROVEEDOR", Atributos: null, CorreoInvitacion: "ana@aceros.co",
    ReferenciaExterna: new ReferenciaExternaDto("SRM", "ADP-00123")),
    claveIdempotencia: "srm-sol-00123");   // un reintento con la misma clave no duplica
```

## Consultar la certificación antes de pagar

```csharp
var r = await validasoft.Consultas.CertificacionAsync(new ConsultaDeCertificacion("NIT", "900123456", "PROVEEDOR"), etag: copia?.ETag);
var datos = r.NoModificado ? copia!.Datos : r.Datos!;     // 304: sigue valiendo tu copia
if (datos.Bloqueado) { /* no pagar: datos.MotivoBloqueo dice por qué */ }
```

## Recibir webhooks

```csharp
var verificador = new VerificadorDeWebhooks(new[] { secretoActual, secretoAnterior });   // el anterior solo durante una rotación
var evento = verificador.Validar(nombre => request.Headers[nombre].FirstOrDefault(), cuerpoCrudo);   // lanza WebhookInvalidoException
if (await entregas.RegistrarAsync(evento.Id)) cola.Encolar(evento);                    // deduplica por el id del evento
```

## Probar en el sandbox

Una API key `vsk_test_…` opera en un entorno aislado: con la configuración de tu grupo, sin datos reales, con los correos retenidos
en un buzón y con los proveedores de IA y de listas simulados.

```csharp
var sandbox = new ClienteValidasoft(http, new OpcionesValidasoft { UrlBase = url, ApiKey = llaveDePrueba, EmpresaId = empresaDeSandbox });
var estado = await sandbox.Sandbox.ObtenerAsync();                         // las empresas del sandbox y su id para X-Empresa
await sandbox.Vinculos.CrearAsync(/* … */);
var correo = await sandbox.Sandbox.EsperarCorreoAsync("ana@aceros.co");   // la invitación no salió: está en el buzón
var enlace = correo.Enlaces.First();                                       // ábrelo para seguir el flujo como el tercero
await sandbox.Sandbox.ReiniciarAsync();                                    // vuelve a empezar de cero
```

## Errores

Toda respuesta de error es una `ExcepcionValidasoft` con `Estado` (HTTP), `Codigo` (estable: decide con él, no con el texto),
`Detalle` y `Correlacion` (cítala al pedir soporte).

## Versionado

[Versionado semántico](https://semver.org/lang/es/) alineado con la API v1: las versiones menores y de parche no rompen nada; la mayor
llega con una API nueva. Los cambios están en [CHANGELOG.md](CHANGELOG.md).
