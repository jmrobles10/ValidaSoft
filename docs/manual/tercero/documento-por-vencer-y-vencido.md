---
id: "tercero/documento-por-vencer-y-vencido"
titulo: "Atiende un documento que está por vencer"
actor: "Tercero"
dominio: "tercero"
resumen: "El certificado de seguridad y salud vence 30 días después de su expedición. Cuando faltan pocos días, la tarjeta lo marca como «Por vencer»; cuando vence, pasa por un periodo de gracia. Subes uno nuevo y el documento vuelve a estar vigente."
pasos: 2
---

# Atiende un documento que está por vencer

**Actor:** Tercero · **Dominio:** Portal del tercero

El certificado de seguridad y salud vence 30 días después de su expedición. Cuando faltan pocos días, la tarjeta lo marca como «Por vencer»; cuando vence, pasa por un periodo de gracia. Subes uno nuevo y el documento vuelve a estar vigente.

**Antes de empezar**

- Un grupo empresarial con «Constructora Sur S.A.S.», su flujo de Proveedor (RUT y certificado de seguridad y salud en el trabajo, con vigencia de 30 días, 10 de aviso y 5 de gracia).
- Un tercero cuyo certificado de seguridad fue expedido hace 25 días, así que queda a 5 días de vencer.

**Pasos**

1. Revisa qué documento está por vencer
2. Sube un certificado nuevo

## Paso 1. Revisa qué documento está por vencer

La tarjeta del certificado dice «Por vencer» y la fecha en que vence. El aviso «Vence pronto» te recuerda que debes subir uno nuevo antes de esa fecha. Si el documento vence y pasa su plazo de gracia, la tarjeta pasa a «Vencido» y la empresa lo ve como vencido.

> **Pantalla:** `/expediente/{id}`

![Paso 1: Revisa qué documento está por vencer (escritorio)](../../../salida/manual/img/tercero/documento-por-vencer-y-vencido/01-revisa-que-documento-esta-por-vencer-claro.webp)

![Paso 1: Revisa qué documento está por vencer (celular)](../../../salida/manual/img/tercero/documento-por-vencer-y-vencido/01-revisa-que-documento-esta-por-vencer-celular.webp)

**Variantes**

- **El documento venció pero aún está dentro del plazo de gracia:** La tarjeta dice «En periodo de gracia» y la fecha hasta la que puedes subir uno nuevo.

## Paso 2. Sube un certificado nuevo

Sube el certificado nuevo con su fecha de expedición reciente. Queda como la versión 2 y el vencimiento se cuenta desde esa fecha. La empresa lo revisa de nuevo, como cualquier versión nueva.

> **Pantalla:** `/expediente/{id}`

![Paso 2: Sube un certificado nuevo (escritorio)](../../../salida/manual/img/tercero/documento-por-vencer-y-vencido/02-sube-un-certificado-nuevo-claro.webp)

![Paso 2: Sube un certificado nuevo (celular)](../../../salida/manual/img/tercero/documento-por-vencer-y-vencido/02-sube-un-certificado-nuevo-celular.webp)

## Recorridos relacionados

- [Sube una versión nueva de un documento ya aprobado](subir-version-nueva.md)
- [Ratifica tu información antes de que venza el vínculo](vinculo-por-vencer-renovar.md)
