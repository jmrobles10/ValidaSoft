---
id: "ia/cupo-agotado"
titulo: "Atiende los documentos cuando se agota el cupo de IA"
actor: "Administrador de empresa"
dominio: "ia"
resumen: "La empresa tiene una sola validación incluida en el mes. El primer RUT la usa y se aprueba solo; el segundo ya no tiene análisis: llega a revisión humana, sin aprobarse solo, y la administradora ve el cupo agotado en «Plan y uso de IA»."
pasos: 3
---

# Atiende los documentos cuando se agota el cupo de IA

**Actor:** Administrador de empresa · **Dominio:** IA y revisión automática

La empresa tiene una sola validación incluida en el mes. El primer RUT la usa y se aprueba solo; el segundo ya no tiene análisis: llega a revisión humana, sin aprobarse solo, y la administradora ve el cupo agotado en «Plan y uso de IA».

**Antes de empezar**

- Un grupo con Constructora Sur, la IA activa con 1 validación incluida al mes y sin adicional, y revisión automática en nivel Automático para el RUT.

**Pasos**

1. Revisa el cupo antes de usarlo
2. Revisa el documento que quedó sin análisis
3. Comprueba el cupo agotado

## Paso 1. Revisa el cupo antes de usarlo

Antes de que lleguen documentos, «Validaciones incluidas» muestra «0 de 1» y «Quedan 1 este mes». Sin adicional, cuando se agote el cupo los documentos nuevos pasan a revisión manual: nada se bloquea.

> **Pantalla:** `/e/{empresa}/uso-ia`

![Paso 1: Revisa el cupo antes de usarlo (escritorio)](../../../salida/manual/img/ia/cupo-agotado/01-revisa-el-cupo-antes-de-usarlo-claro.webp)

## Paso 2. Revisa el documento que quedó sin análisis

El segundo RUT llegó cuando el cupo ya estaba agotado. Su documento dice «Sin análisis automático: revísalo manualmente» y la decisión automática indica que requiere una persona por «Sin análisis de IA». Nada se aprueba solo: el área lo decide como cualquier documento.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Revisa el documento que quedó sin análisis (escritorio)](../../../salida/manual/img/ia/cupo-agotado/02-revisa-el-documento-que-quedo-sin-analisis-claro.webp)

**Variantes**

- **Se agota el cupo y la empresa tiene adicional:** Los documentos se analizan con el adicional, y solo cuando este también se agota quedan sin análisis.

## Paso 3. Comprueba el cupo agotado

El plan muestra «1 de 1» y el aviso «Agotaste las incluidas de este mes». Los administradores reciben aviso al llegar al 80 % y al 100 % del cupo. El tercero no ve nada de esto: ni la confianza ni el modelo de IA.

> **Pantalla:** `/e/{empresa}/uso-ia`

![Paso 3: Comprueba el cupo agotado (escritorio)](../../../salida/manual/img/ia/cupo-agotado/03-comprueba-el-cupo-agotado-claro.webp)

## Recorridos relacionados

- [Revisa el plan y el uso de IA de la empresa](plan-y-uso-de-ia.md)
- [Revisa un documento que el sistema aprobó solo](revision-automatica-aprueba-sola.md)
