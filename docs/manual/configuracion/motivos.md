---
id: "configuracion/motivos"
titulo: "Crea un motivo de observación propio y lo desactiva"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora agrega a los motivos del grupo uno propio, «Foto del documento borrosa», con el texto que lee el proveedor. Luego lo desactiva para que los revisores ya no lo puedan elegir."
pasos: 5
---

# Crea un motivo de observación propio y lo desactiva

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora agrega a los motivos del grupo uno propio, «Foto del documento borrosa», con el texto que lee el proveedor. Luego lo desactiva para que los revisores ya no lo puedan elegir.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y los motivos de la plataforma ya cargados.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Motivos
2. Define el motivo y el texto para el proveedor
3. Revisa el motivo en la tabla
4. Desactiva el motivo
5. Comprueba el motivo desactivado

## Paso 1. Abre Configuración › Motivos

Un motivo es la razón tipificada por la que un revisor observa o rechaza un expediente. Los de la plataforma son fijos y se marcan con «Plataforma»; el grupo puede agregar los suyos. La columna «Texto al tercero» es lo único que lee el proveedor: nunca ve el nombre del motivo ni quién lo eligió.

> **Pantalla:** `/e/{empresa}/motivos`

![Paso 1: Abre Configuración › Motivos (escritorio)](../../../salida/manual/img/configuracion/motivos/01-abre-configuracion-motivos-claro.webp)

**Configuración que lo afecta**

- **Motivos de observación y rechazo** (`Configuración › Motivos (/motivos)`): Los revisores eligen de esta lista cuando observan o rechazan un expediente.

## Paso 2. Define el motivo y el texto para el proveedor

El código (minúsculas, números y guiones) y el nombre identifican el motivo para los revisores; el código no se puede cambiar. «Categoría» agrupa el motivo y «Se usa al» dice si sirve para observar, rechazar o ambos. El texto es lo que el proveedor lee: dile qué corregir y cómo, sin nombrar áreas ni personas.

> **Pantalla:** `/e/{empresa}/motivos`

![Paso 2: Define el motivo y el texto para el proveedor (escritorio)](../../../salida/manual/img/configuracion/motivos/02-define-el-motivo-y-el-texto-para-el-proveedor-claro.webp)

**Variantes**

- **Se usa al «Observar»:** Los revisores lo pueden elegir para devolver el expediente, pero no para rechazarlo.
- **Se usa al «Observar y rechazar»:** El motivo aparece en ambas acciones.

## Paso 3. Revisa el motivo en la tabla

El motivo nuevo aparece con la marca de su categoría y la acción en la que se usa. A diferencia de los de la plataforma, sí puedes editarlo: con el botón de lápiz de su fila.

> **Pantalla:** `/e/{empresa}/motivos`

![Paso 3: Revisa el motivo en la tabla (escritorio)](../../../salida/manual/img/configuracion/motivos/03-revisa-el-motivo-en-la-tabla-claro.webp)

## Paso 4. Desactiva el motivo

El interruptor «Disponible para los revisores» decide si el motivo se ofrece al observar o rechazar. Apagado, el motivo sigue en la tabla para consultar los expedientes que ya lo usaron, pero ningún revisor puede elegirlo.

> **Pantalla:** `/e/{empresa}/motivos`

![Paso 4: Desactiva el motivo (escritorio)](../../../salida/manual/img/configuracion/motivos/04-desactiva-el-motivo-claro.webp)

**Variantes**

- **El motivo es de la plataforma:** No hay lápiz: los motivos de la plataforma no se modifican.

## Paso 5. Comprueba el motivo desactivado

El motivo queda con estado «Inactivo» en la tabla. Los expedientes que ya fueron observados con él no cambian, y desde ahora no aparece en la lista al observar o rechazar.

> **Pantalla:** `/e/{empresa}/motivos`

![Paso 5: Comprueba el motivo desactivado (escritorio)](../../../salida/manual/img/configuracion/motivos/05-comprueba-el-motivo-desactivado-claro.webp)

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](tipo-de-documento-y-flujo.md)
