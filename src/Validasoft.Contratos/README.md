# Validasoft.Contratos

Los contratos (DTO) de la API v1 de [Validasoft](https://github.com/jmrobles10/ValidaSoft): los tipos que viajan en las peticiones y
respuestas de la API, en los eventos públicos y en los webhooks. Multi-target `net7.0`, `net8.0` y `net10.0`, sin dependencias.

La mayoría de los sistemas integradores **no instala este paquete a mano**: viene con
[`Validasoft.Sdk`](https://www.nuget.org/packages/Validasoft.Sdk), el cliente tipado de la API. Instálalo por separado solo si prefieres
escribir tu propio cliente HTTP y quieres los mismos tipos:

```bash
dotnet add package Validasoft.Contratos
```

```csharp
using Validasoft.Contratos;

var evento = JsonSerializer.Deserialize<EventoPublicoDto>(cuerpoDelWebhook, new JsonSerializerOptions(JsonSerializerDefaults.Web));
if (evento is { Tipo: "certificado.emitido" })
{
    var datos = evento.Datos;   // JsonElement: el detalle de cada tipo está en la guía de integración
}
```

Los nombres siguen el dominio en español (`VinculoDto`, `ConsultaCertificacionDto`, `EventoPublicoDto`…). Las propiedades se serializan en
`camelCase` y las fechas en ISO 8601.

Versionado semántico alineado con la API v1; los cambios están en [CHANGELOG.md](CHANGELOG.md). La guía de integración completa:
[docs/integracion.md](https://github.com/jmrobles10/ValidaSoft/blob/main/docs/integracion.md).
