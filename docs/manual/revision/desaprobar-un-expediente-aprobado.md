---
id: "revision/desaprobar-un-expediente-aprobado"
titulo: "Desaprueba un expediente aprobado"
actor: "Administrador de empresa"
dominio: "revision"
resumen: "Ana descubre una inhabilidad después de aprobar a un aliado. Desaprueba el expediente: el vínculo vuelve a validación, su certificado se revoca y el proveedor recibe un aviso sin la justificación interna. Al aprobarlo de nuevo se emite otro certificado."
pasos: 4
---

# Desaprueba un expediente aprobado

**Actor:** Administrador de empresa · **Dominio:** Revisión de expedientes

Ana descubre una inhabilidad después de aprobar a un aliado. Desaprueba el expediente: el vínculo vuelve a validación, su certificado se revoca y el proveedor recibe un aviso sin la justificación interna. Al aprobarlo de nuevo se emite otro certificado.

**Antes de empezar**

- Un grupo con Constructora Norte, con las áreas Compras (Rubén) y Cumplimiento (Olga). Ana es administradora.
- Un flujo de Aliado con dos etapas en secuencia. El vínculo ya está aprobado y tiene su certificado vigente.

**Pasos**

1. Abre un expediente ya aprobado
2. Desaprueba el expediente
3. Revisa el expediente desaprobado
4. Así se entera el proveedor

## Paso 1. Abre un expediente ya aprobado

El expediente aprobado muestra el aviso «Expediente aprobado» con la fecha de aprobación. Las dos etapas están aprobadas. Desde «Más acciones» quien administra la empresa puede desaprobarlo si aparece un hallazgo que cambia la decisión.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre un expediente ya aprobado (escritorio)](../../../salida/manual/img/revision/desaprobar-un-expediente-aprobado/01-abre-un-expediente-ya-aprobado-claro.webp)

**Variantes**

- **Eres revisor de un área:** No ves «Más acciones» ni la opción de desaprobar: solo un administrador puede desaprobar.

## Paso 2. Desaprueba el expediente

La desaprobación dice qué pasa: el mismo expediente vuelve a revisión, el vínculo pasa a En validación y su certificado se revoca. Eliges desde qué etapa se revisa de nuevo: la última, la primera o una etapa concreta. Las etapas anteriores conservan su aprobación. La justificación es interna.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Desaprueba el expediente (escritorio)](../../../salida/manual/img/revision/desaprobar-un-expediente-aprobado/02-desaprueba-el-expediente-claro.webp)

**Configuración que lo afecta**

- **Desde qué etapa se revisa de nuevo**: Las etapas anteriores a la elegida conservan su aprobación.

## Paso 3. Revisa el expediente desaprobado

El expediente ya no está aprobado: la etapa 1 conserva su aprobación y la etapa 2 vuelve a «En curso». El vínculo queda en validación con este mismo expediente. Cuando el área vuelva a aprobar, se emite un certificado nuevo y el anterior queda como revocado.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Revisa el expediente desaprobado (escritorio)](../../../salida/manual/img/revision/desaprobar-un-expediente-aprobado/03-revisa-el-expediente-desaprobado-claro.webp)

## Paso 4. Así se entera el proveedor

El proveedor recibe un correo que dice que la empresa volvió a revisar su validación. La justificación interna no sale en el correo: el proveedor solo sabe que se revisa de nuevo.

> **Pantalla:** `/`

![Paso 4: Así se entera el proveedor (escritorio)](../../../salida/manual/img/revision/desaprobar-un-expediente-aprobado/04-asi-se-entera-el-proveedor-claro.webp)

## Recorridos relacionados

- [Reabre una etapa ya aprobada](desaprobar-y-reabrir-una-etapa.md)
