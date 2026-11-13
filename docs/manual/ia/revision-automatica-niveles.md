---
id: "ia/revision-automatica-niveles"
titulo: "Elige el nivel de revisión automática con IA"
actor: "Administrador de empresa"
dominio: "ia"
resumen: "La administradora decide cuánto hace la IA en cada documento: Manual (nada), Asistido (la IA lee y una persona decide) o Automático (el sistema aprueba lo limpio). Elige los tipos de documento y las áreas que alcanza, y el porcentaje de muestra que una persona confirma."
pasos: 4
---

# Elige el nivel de revisión automática con IA

**Actor:** Administrador de empresa · **Dominio:** IA y revisión automática

La administradora decide cuánto hace la IA en cada documento: Manual (nada), Asistido (la IA lee y una persona decide) o Automático (el sistema aprueba lo limpio). Elige los tipos de documento y las áreas que alcanza, y el porcentaje de muestra que una persona confirma.

**Antes de empezar**

- Un grupo con Constructora Sur, la IA activa para la empresa y un flujo de Cliente que pide el RUT al área Revisión.

**Pasos**

1. Abre la revisión automática
2. Elige Automático y lo que alcanza
3. Define la muestra para confirmar
4. Confirma antes de activar

## Paso 1. Abre la revisión automática

Sin configuración guardada, la empresa está en Manual. Manual: la IA analiza y solo informa; cada área revisa todo, como hasta ahora. Asistido: la IA sugiere aprobar o revisar cada documento, pero nunca aprueba: decide siempre una persona. Automático: la IA aprueba sola lo que no tiene excepciones, una persona confirma una muestra y atiende el resto.

> **Pantalla:** `/e/{empresa}/revision-automatica`

![Paso 1: Abre la revisión automática (escritorio)](../../../salida/manual/img/ia/revision-automatica-niveles/01-abre-la-revision-automatica-claro.webp)

**Configuración que lo afecta**

- **Nivel de revisión automática** (`Configuración › Revisión automática (/revision-automatica)`): Define si la IA participa en la revisión de los documentos de la empresa y qué puede decidir sola.

## Paso 2. Elige Automático y lo que alcanza

En Automático el sistema aprueba los documentos limpios sin que una persona los vea, salvo el muestreo. Solo lo que elijas: «Tipos de documento» y «Áreas cuya revisión hace el sistema». Un documento dudoso nunca se aprueba solo: pasa a Excepciones para una persona. Sin tipos ni áreas no se puede guardar.

> **Pantalla:** `/e/{empresa}/revision-automatica`

![Paso 2: Elige Automático y lo que alcanza (escritorio)](../../../salida/manual/img/ia/revision-automatica-niveles/02-elige-automatico-y-lo-que-alcanza-claro.webp)

## Paso 3. Define la muestra para confirmar

El porcentaje de muestra es la parte de los documentos aprobados solos que una persona confirma después, en Excepciones. Con 0 %, todo lo limpio se aprueba sin revisión humana; con 100 %, todo lo aprobado por el sistema queda para confirmar.

> **Pantalla:** `/e/{empresa}/revision-automatica`

![Paso 3: Define la muestra para confirmar (escritorio)](../../../salida/manual/img/ia/revision-automatica-niveles/03-define-la-muestra-para-confirmar-claro.webp)

**Variantes**

- **La muestra es mayor que 0:** Algunos documentos limpios aparecen en Excepciones como «Muestreo» para que una persona los confirme.

## Paso 4. Confirma antes de activar

Antes de guardar, el aviso dice cuántos tipos de documento y cuántas áreas quedan bajo aprobación automática. Solo al confirmar se guarda el nivel Automático.

> **Pantalla:** `/e/{empresa}/revision-automatica`

![Paso 4: Confirma antes de activar (escritorio)](../../../salida/manual/img/ia/revision-automatica-niveles/04-confirma-antes-de-activar-claro.webp)

## Recorridos relacionados

- [Revisa un documento que el sistema aprobó solo](revision-automatica-aprueba-sola.md)
- [Atiende una excepción de la revisión asistida](revision-asistida-y-excepciones.md)
