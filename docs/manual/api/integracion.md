# API · Integración

1 operaciones en 1 recurso.

## Consulta certificacion

Consulta rápida de certificación para Marco y los ERP (H9, docs/05 §2.6 y §5.6, docs/08 §5; CN-1, CN-2): por identificación del tercero y tipo de vínculo (o por id del vínculo) en la empresa de `X-Empresa`, ¿tiene certificado, en qué estado, hasta cuándo y está bloqueado? Son tres viajes a la base por índices únicos o llave primaria (tercero y tipo de vínculo en un lote; el vínculo por `ux_vinculo_natural`; certificado y expediente de actualización en otro lote), sin proyección nueva. Responde `ETag` (huella del cuerpo) y `If-None-Match` ⇒ 304.

### `GET /api/v1/consultas/certificacion`

**Consultar certificacion.** `?pais=CO&tipoIdentificacion=NIT&numero=900123456&tipoVinculo=PROVEEDOR` o `?vinculoId=`. El número se normaliza (puntos, guiones y DV se aceptan). Sin `tipoVinculo` sirve si el tercero tiene un solo vínculo en la empresa (si tiene varios: 422 `tipo-vinculo-requerido`). Sin vínculo, o con un tercero que no existe: 200 con `estadoVinculo: "SinVinculo"` (no revela si el tercero está en la plataforma).

**Permiso consultas:leer · API (`/api/v1`) · SDK: `consultarCertificacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `pais` | query | texto | no |  |
| `tipoIdentificacion` | query | texto | no |  |
| `numero` | query | texto | no |  |
| `tipoVinculo` | query | texto | no |  |
| `vinculoId` | query | uuid | no |  |

**Respuestas**

- `200` OK — ConsultaCertificacionDto
- `304` Sin cambios

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `certificado` | booleano | sí |  |
| `folio` | texto o null | no |  |
| `estado` | texto o null | no |  |
| `estadoVinculo` | texto | sí |  |
| `vinculoId` | uuid o null | no |  |
| `terceroId` | uuid o null | no |  |
| `vigenteHasta` | fecha o null | no |  |
| `revalidacionPendiente` | lista de RevalidacionPendienteDto | sí |  |
| `revalidacionPendiente[].tipo` | texto | sí |  |
| `revalidacionPendiente[].plazo` | fecha o null | no |  |
| `revalidacionPendiente[].bloqueante` | booleano | sí |  |
| `documentosVencidos` | lista de texto | sí |  |
| `bloqueado` | booleano | sí |  |
| `motivoBloqueo` | texto o null | no |  |
| `cuentaDesignada` | CuentaDesignadaResumenDto o null | no |  |
| `cuentaDesignada.numeroEnmascarado` | texto | sí |  |
| `cuentaDesignada.banco` | texto | sí |  |
| `cuentaDesignada.bancoCodigo` | texto o null | no |  |
| `cuentaDesignada.bancoSwift` | texto o null | no |  |
| `urlVerificacion` | texto o null | no |  |
| `legacy` | LegadoDto o null | no |  |
| `legacy.estadoTercero` | entero | sí |  |
| `fuenteRespaldo` | booleano | no | por omisión false |
| `antiguedadSegundos` | entero o null | no |  |
| `enRevision` | booleano | no | por omisión false |
| `enVerificacion` | booleano | no | por omisión false |

**Errores de negocio que lanza**

- `404` `vinculo-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/consultas/certificacion" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "certificado": true,
  "folio": "texto",
  "estado": "texto",
  "estadoVinculo": "texto",
  "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vigenteHasta": "2026-11-12",
  "revalidacionPendiente": [
    {
      "tipo": "texto",
      "plazo": "2026-11-12",
      "bloqueante": true
    }
  ],
  "documentosVencidos": [
    "texto"
  ],
  "bloqueado": true,
  "motivoBloqueo": "Texto de ejemplo.",
  "cuentaDesignada": {
    "numeroEnmascarado": "900123456",
    "banco": "texto",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto"
  },
  "urlVerificacion": "https://ejemplo.co/recurso",
  "legacy": {
    "estadoTercero": 1
  },
  "fuenteRespaldo": false,
  "antiguedadSegundos": 1,
  "enRevision": false,
  "enVerificacion": false
}
```

</details>
