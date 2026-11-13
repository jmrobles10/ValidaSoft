---
id: "vinculos/abrir-expediente-manual"
titulo: "Abre el expediente sin esperar a que el tercero acepte"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora abre a mano el expediente de un proveedor invitado que todavía no ha aceptado la invitación, para que las áreas empiecen la validación sin esperar al tercero."
pasos: 3
---

# Abre el expediente sin esperar a que el tercero acepte

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora abre a mano el expediente de un proveedor invitado que todavía no ha aceptado la invitación, para que las áreas empiecen la validación sin esperar al tercero.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Proveedor publicado como flujo por defecto.
- Un proveedor invitado que aún no ha aceptado la invitación.

**Pasos**

1. Encuentra la tarjeta Expediente del vínculo
2. Revisa el flujo con el que se abre
3. Comprueba que el expediente quedó abierto

## Paso 1. Encuentra la tarjeta Expediente del vínculo

Mientras el tercero no ha aceptado, la tarjeta «Expediente» dice «Sin expediente abierto» y ofrece el botón «Abrir expediente». Usa esa opción cuando necesitas empezar la validación ya, por ejemplo porque el tercero entregó sus documentos por otro canal.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Encuentra la tarjeta Expediente del vínculo (escritorio)](../../../salida/manual/img/vinculos/abrir-expediente-manual/01-encuentra-la-tarjeta-expediente-del-vinculo-claro.webp)

**Variantes**

- **El tercero ya aceptó la invitación:** La tarjeta muestra el expediente en curso y el botón «Ver expediente»; no hace falta abrirlo a mano.
- **El tipo de vínculo no tiene un flujo publicado:** El diálogo dice que no hay flujos publicados y te manda a Configuración › Flujos aprobatorios.

## Paso 2. Revisa el flujo con el que se abre

El diálogo propone el flujo por defecto del tipo de vínculo, con sus etapas y áreas. Ese flujo define qué áreas revisarán los documentos del proveedor. Al confirmar, el vínculo pasa a «En validación» y las áreas ven el expediente en su bandeja, aunque el tercero todavía no haya aceptado la invitación.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Revisa el flujo con el que se abre (escritorio)](../../../salida/manual/img/vinculos/abrir-expediente-manual/02-revisa-el-flujo-con-el-que-se-abre-claro.webp)

**Configuración que lo afecta**

- **Flujo por defecto por tipo de vínculo** (`Configuración › Flujos aprobatorios (/flujos)`): Es el flujo que se propone al abrir el expediente a mano.

## Paso 3. Comprueba que el expediente quedó abierto

Validasoft confirma con el folio del expediente y te lleva a su pantalla, con «Datos del expediente» y el estado de cada área. La invitación sigue pendiente: cuando el tercero acepte, el mismo expediente continúa y no se vuelve a empezar.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Comprueba que el expediente quedó abierto (escritorio)](../../../salida/manual/img/vinculos/abrir-expediente-manual/03-comprueba-que-el-expediente-quedo-abierto-claro.webp)

## Recorridos relacionados

- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
- [Aprueba el área de Compras](../revision/aprobar-un-area.md)
