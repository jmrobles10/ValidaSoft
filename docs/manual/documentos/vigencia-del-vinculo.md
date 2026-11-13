---
id: "documentos/vigencia-del-vinculo"
titulo: "Sigue la vigencia periódica de un vínculo"
actor: "Administrador de empresa"
dominio: "documentos"
resumen: "La administradora de Constructora Sur ve cómo cambia un cliente cuyo vínculo tiene vigencia: activo y con fecha de vencimiento, por vencer con su renovación abierta, y vencido cuando el tercero no ratifica a tiempo. El certificado sigue la misma suerte."
pasos: 4
---

# Sigue la vigencia periódica de un vínculo

**Actor:** Administrador de empresa · **Dominio:** Documentos y vigencias

La administradora de Constructora Sur ve cómo cambia un cliente cuyo vínculo tiene vigencia: activo y con fecha de vencimiento, por vencer con su renovación abierta, y vencido cuando el tercero no ratifica a tiempo. El certificado sigue la misma suerte.

**Antes de empezar**

- Un grupo con Constructora Sur y un flujo de Cliente con RUT e información general, cuyo vínculo vale 90 días, avisa a los 30 y 5 días y tiene 3 de gracia.
- Un cliente jurídico aprobado por el área Revisión hoy: su vínculo queda activo hasta dentro de 90 días.

**Pasos**

1. Mira el vínculo activo en la lista
2. Revisa hasta cuándo vale la validación
3. Atiende el pre-vencimiento
4. Mira el vínculo vencido

## Paso 1. Mira el vínculo activo en la lista

El cliente aparece en la lista de vínculos de Constructora Sur con el estado «Activo». Mientras su validación esté vigente, el vínculo sigue operando normal, sin avisos. La fecha «Vigente hasta» vive dentro del vínculo: ábrelo para verla.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 1: Mira el vínculo activo en la lista (escritorio)](../../../salida/manual/img/documentos/vigencia-del-vinculo/01-mira-el-vinculo-activo-en-la-lista-claro.webp)

**Configuración que lo afecta**

- **Vigencia del vínculo (por flujo)** (`Configuración › Flujos aprobatorios (/flujos)`): Cada cuántos días se vuelve a validar a los terceros de ese flujo, y cuándo se avisa antes de vencer.

## Paso 2. Revisa hasta cuándo vale la validación

En el detalle, «Vigente hasta» muestra la fecha en que vence la validación del tercero. La vigencia se cuenta desde la última aprobación, así que una renovación la vuelve a contar desde cero. Mientras falte más que el primer aviso, no hay nada que hacer.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Revisa hasta cuándo vale la validación (escritorio)](../../../salida/manual/img/documentos/vigencia-del-vinculo/02-revisa-hasta-cuando-vale-la-validacion-claro.webp)

## Paso 3. Atiende el pre-vencimiento

Treinta días antes de vencer, el vínculo pasa a «Por vencer» y el aviso dice la fecha de vencimiento. Se abre sola una renovación y el tercero recibe un correo para ratificar su información. El certificado sigue vigente mientras tanto. Si el tercero ratifica o actualiza antes de la fecha, el vínculo se renueva y no vence.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Atiende el pre-vencimiento (escritorio)](../../../salida/manual/img/documentos/vigencia-del-vinculo/03-atiende-el-pre-vencimiento-claro.webp)

## Paso 4. Mira el vínculo vencido

Si el tercero no ratifica a tiempo y terminó la gracia de 3 días, el vínculo pasa a «Vencido» y su certificado también. La renovación que estaba abierta sigue siendo la misma: el tercero la cierra ratificando su información, y el vínculo vuelve a estar activo con una vigencia nueva de 90 días.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 4: Mira el vínculo vencido (escritorio)](../../../salida/manual/img/documentos/vigencia-del-vinculo/04-mira-el-vinculo-vencido-claro.webp)

## Recorridos relacionados

- [Ratifica tu información antes de que venza el vínculo](../tercero/vinculo-por-vencer-renovar.md)
- [Define la vigencia periódica del vínculo](../configuracion/flujo-vigencia-del-vinculo.md)
- [Sigue el ciclo de vigencia de un certificado](ciclo-de-vigencia.md)
