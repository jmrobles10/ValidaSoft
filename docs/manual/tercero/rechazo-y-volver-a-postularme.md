---
id: "tercero/rechazo-y-volver-a-postularme"
titulo: "Vuelve a postularte después de un rechazo"
actor: "Tercero"
dominio: "tercero"
resumen: "Cumplimiento rechaza tu expediente y te dice el motivo, sin mostrarte la justificación interna. Desde «Mis empresas» vuelves a postularte: tus documentos ya están en tu bóveda, subes el que falló corregido y se abre una revisión nueva."
pasos: 3
---

# Vuelve a postularte después de un rechazo

**Actor:** Tercero · **Dominio:** Portal del tercero

Cumplimiento rechaza tu expediente y te dice el motivo, sin mostrarte la justificación interna. Desde «Mis empresas» vuelves a postularte: tus documentos ya están en tu bóveda, subes el que falló corregido y se abre una revisión nueva.

**Antes de empezar**

- Un grupo empresarial con «Constructora Norte», su flujo de Proveedor (RUT y Cámara de comercio) con reintento permitido sin espera, un área «Compras» y un área «Cumplimiento» que puede rechazar.
- Un tercero que subió sus dos documentos y cuyo expediente fue rechazado por Cumplimiento con un motivo.

**Pasos**

1. Lee por qué te rechazaron
2. Vuelve a postularte con los mismos documentos
3. Sube la versión corregida del documento que falló

## Paso 1. Lee por qué te rechazaron

La tarjeta de la empresa dice que cerró tu revisión sin aprobarla y muestra el motivo que escogió el área, en este caso que el documento no corresponde a la persona o empresa que se valida. Lo que no ves es la justificación interna que escribió la empresa ni quién la escribió: esa parte es privada. Tus documentos siguen en tu bóveda.

> **Pantalla:** `/empresas`

![Paso 1: Lee por qué te rechazaron (escritorio)](../../../salida/manual/img/tercero/rechazo-y-volver-a-postularme/01-lee-por-que-te-rechazaron-claro.webp)

![Paso 1: Lee por qué te rechazaron (celular)](../../../salida/manual/img/tercero/rechazo-y-volver-a-postularme/01-lee-por-que-te-rechazaron-celular.webp)

## Paso 2. Vuelve a postularte con los mismos documentos

Al pulsar «Volver a postularme» se abre una revisión nueva para la misma empresa. El RUT y la Cámara de comercio aparecen con la versión 1 que ya estaba en tu bóveda, así que no tienes que cargarlos de nuevo. Solo sube otra versión del documento que la empresa rechazó.

> **Pantalla:** `/expediente/{id}`

![Paso 2: Vuelve a postularte con los mismos documentos (escritorio)](../../../salida/manual/img/tercero/rechazo-y-volver-a-postularme/02-vuelve-a-postularte-con-los-mismos-documentos-claro.webp)

![Paso 2: Vuelve a postularte con los mismos documentos (celular)](../../../salida/manual/img/tercero/rechazo-y-volver-a-postularme/02-vuelve-a-postularte-con-los-mismos-documentos-celular.webp)

**Configuración que lo afecta**

- **Después de un rechazo › Permitir que el tercero rechazado vuelva a postularse** (`Configuración › Flujos aprobatorios (/flujos)`): Con Días de espera, el botón aparece solo cuando pasan esos días desde el rechazo; con 0, de inmediato.

## Paso 3. Sube la versión corregida del documento que falló

En la tarjeta del RUT pulsa «Subir» y elige el archivo corregido. Queda como la versión 2 y la nueva revisión la recibe junto con los demás documentos. Las áreas revisan el expediente nuevo y deciden por separado, como en la primera revisión.

> **Pantalla:** `/expediente/{id}`

![Paso 3: Sube la versión corregida del documento que falló (escritorio)](../../../salida/manual/img/tercero/rechazo-y-volver-a-postularme/03-sube-la-version-corregida-del-documento-que-fall-claro.webp)

![Paso 3: Sube la versión corregida del documento que falló (celular)](../../../salida/manual/img/tercero/rechazo-y-volver-a-postularme/03-sube-la-version-corregida-del-documento-que-fall-celular.webp)

## Recorridos relacionados

- [Ve que tu expediente quedó aprobado](expediente-aprobado.md)
- [Corrige lo que la empresa observó y responde](responder-observaciones.md)
