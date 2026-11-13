---
id: "documentos/documento-compartido-entre-empresas"
titulo: "Revisa una versión nueva de un documento que comparten dos empresas"
actor: "Revisor/área"
dominio: "documentos"
resumen: "Sofía, del área Revisión de Constructora Sur, ve que el tercero cambió su información después de que ella la aprobó. Su revalidación muestra solo lo que cambió. Lo que decide Constructora Norte no aparece en su pantalla."
pasos: 3
---

# Revisa una versión nueva de un documento que comparten dos empresas

**Actor:** Revisor/área · **Dominio:** Documentos y vigencias

Sofía, del área Revisión de Constructora Sur, ve que el tercero cambió su información después de que ella la aprobó. Su revalidación muestra solo lo que cambió. Lo que decide Constructora Norte no aparece en su pantalla.

**Antes de empezar**

- Dos empresas del grupo, Constructora Norte y Constructora Sur, con un tipo de vínculo «Aliado» que exige el RUT y la información general.
- Un tercero que aceptó a las dos empresas y que ambas aprobaron en su versión 1. Después subió un RUT nuevo y cambió su dirección; Norte ya aprobó esa versión y Sur no.

**Pasos**

1. Mira la ficha que aceptaste del tercero
2. Compara lo que cambió
3. Aprueba la revisión de tu área

## Paso 1. Mira la ficha que aceptaste del tercero

En la ficha aceptada de Sur se ve lo que la empresa aprobó de cada sección: la información general quedó «Aceptada · versión 1». Aparte, el aviso dice que una sección tiene una versión nueva pendiente de revisión, porque el tercero cambió algo después de tu aprobación.

> **Pantalla:** `/e/{empresa}/vinculos/{id}/ficha`

![Paso 1: Mira la ficha que aceptaste del tercero (escritorio)](../../../salida/manual/img/documentos/documento-compartido-entre-empresas/01-mira-la-ficha-que-aceptaste-del-tercero-claro.webp)

## Paso 2. Compara lo que cambió

La tabla «Qué cambió frente a la versión 1 aceptada» muestra cada campo con su valor anterior y el nuevo. Aquí, la dirección pasó de «Calle 45 # 12-08» a «Calle 100 # 19-54». Con esa tabla decides si aceptas el cambio de la sección.

> **Pantalla:** `/e/{empresa}/vinculos/{id}/ficha`

![Paso 2: Compara lo que cambió (escritorio)](../../../salida/manual/img/documentos/documento-compartido-entre-empresas/02-compara-lo-que-cambio-claro.webp)

## Paso 3. Aprueba la revisión de tu área

La revalidación de tu área sigue abierta hasta que la decidas: el botón «Aprobar área» aprueba los cambios del tercero para Constructora Sur. Tu pantalla muestra solo esta revisión: no muestra la decisión que tomó Constructora Norte ni quién de Norte la tomó, porque cada empresa decide en privado.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Aprueba la revisión de tu área (escritorio)](../../../salida/manual/img/documentos/documento-compartido-entre-empresas/03-aprueba-la-revision-de-tu-area-claro.webp)

## Recorridos relacionados

- [Entiende un documento que comparten dos empresas](../tercero/boveda-documento-compartido.md)
- [Aprueba el área de Compras](../revision/aprobar-un-area.md)
- [Revisa los documentos que vencen en la lista de vencimientos](vencimientos.md)
