---
id: "publico/certificado-en-otros-estados"
titulo: "Lee el estado de un certificado suspendido o revocado"
actor: "Público"
dominio: "publico"
resumen: "Cuando la empresa suspende el vínculo o revoca el certificado, el enlace de verificación lo dice con un título propio y su consecuencia: quien lo consulta no debe aceptarlo, no se entrega el PDF y la página no revela el motivo interno."
pasos: 2
---

# Lee el estado de un certificado suspendido o revocado

**Actor:** Público · **Dominio:** Verificación pública

Cuando la empresa suspende el vínculo o revoca el certificado, el enlace de verificación lo dice con un título propio y su consecuencia: quien lo consulta no debe aceptarlo, no se entrega el PDF y la página no revela el motivo interno.

**Antes de empezar**

- Un proveedor aprobado por Constructora Sur con su certificado vigente.
- La administradora de Constructora Sur bloquea el vínculo y después revoca el certificado.

**Pasos**

1. Lee que el certificado está suspendido
2. Lee que el certificado fue revocado

## Paso 1. Lee que el certificado está suspendido

El estado dice «Este certificado está suspendido» y explica desde cuándo la empresa suspendió temporalmente el vínculo: mientras siga así, no lo aceptes. El motivo que la empresa escribió es interno y no aparece aquí. El PDF tampoco se entrega mientras el certificado no esté vigente.

> **Pantalla:** `/verificar/SZCB51-C-2026-000001-1`

![Paso 1: Lee que el certificado está suspendido (escritorio)](../../../salida/manual/img/publico/certificado-en-otros-estados/01-lee-que-el-certificado-esta-suspendido-claro.webp)

**Variantes**

- **El certificado está en verificación:** La página dice «Este certificado está en verificación» y explica que la empresa revisa de nuevo lo que el titular actualizó; sigue vigente hasta su vencimiento.
- **El certificado tiene pendientes:** La página dice «Este certificado tiene pendientes» y pide confirmarlo con la empresa antes de aceptarlo.
- **Hay una emisión más reciente:** La página dice «Hay una emisión más reciente» y ofrece «Ver la emisión actual» con su folio.
- **El certificado venció:** La página dice «Este certificado venció» con la fecha y pide un certificado actualizado.

## Paso 2. Lee que el certificado fue revocado

Un certificado revocado dice «Este certificado fue revocado», la fecha desde la que dejó de valer y que no puede volver a usarse. La empresa puede decirte el motivo; la justificación que escribió no aparece en la página. Si el enlace es auténtico, la sección de autenticidad lo sigue confirmando, pero el certificado ya no sirve.

> **Pantalla:** `/verificar/SZCB51-C-2026-000001-1`

![Paso 2: Lee que el certificado fue revocado (escritorio)](../../../salida/manual/img/publico/certificado-en-otros-estados/02-lee-que-el-certificado-fue-revocado-claro.webp)

## Recorridos relacionados

- [Verifica que un certificado es auténtico](verificar-certificado-autentico.md)
- [Bloquea un vínculo y luego lo desbloquea](../vinculos/bloquear-y-desbloquear.md)
