---
id: "configuracion/flujo-vigencia-del-vinculo"
titulo: "Define la vigencia periódica del vínculo"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora hace que la validación de los proveedores venza cada 365 días, con avisos a 30, 15 y 5 días antes y diez días de gracia. Al llegar el primer aviso, el proveedor ratifica o actualiza la información que pide el flujo."
pasos: 3
---

# Define la vigencia periódica del vínculo

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora hace que la validación de los proveedores venza cada 365 días, con avisos a 30, 15 y 5 días antes y diez días de gracia. Al llegar el primer aviso, el proveedor ratifica o actualiza la información que pide el flujo.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con miembros y la sección «Información general» disponible.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Mira la vigencia del vínculo antes de llenarla
2. Define los días, los avisos y la gracia
3. Guarda la vigencia en el borrador

## Paso 1. Mira la vigencia del vínculo antes de llenarla

La vigencia periódica cuenta días desde la última aprobación del vínculo, no desde que se creó. Cuando faltan los días de aviso, el vínculo queda «Por vencer» y el tercero debe ratificar la información o actualizar las secciones que pide el flujo. Si la validación se aprueba a tiempo, no vence; si no, pasa a vencida al terminar la gracia.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 1: Mira la vigencia del vínculo antes de llenarla (escritorio)](../../../salida/manual/img/configuracion/flujo-vigencia-del-vinculo/01-mira-la-vigencia-del-vinculo-antes-de-llenarla-claro.webp)

**Variantes**

- **Días es 0:** El vínculo no vence por tiempo: los avisos y la gracia se desactivan.

**Configuración que lo afecta**

- **Vigencia del vínculo (por flujo)** (`Configuración › Flujos aprobatorios (/flujos)`): Decide cada cuánto se vuelve a validar a cada proveedor de este flujo.

## Paso 2. Define los días, los avisos y la gracia

«Días» es la vigencia: 365 significa que cada año el proveedor vuelve a validarse. «Avisos» son los días antes de vencer en que se avisa, separados por coma: el primero abre el pre-vencimiento y el vínculo queda «Por vencer». «Gracia» son los días que sigue valiendo el vínculo después de la fecha de vencimiento, mientras el proveedor renueva.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 2: Define los días, los avisos y la gracia (escritorio)](../../../salida/manual/img/configuracion/flujo-vigencia-del-vinculo/02-define-los-dias-los-avisos-y-la-gracia-claro.webp)

**Variantes**

- **Un aviso es mayor que la vigencia:** El campo de avisos lo marca y no se puede guardar.
- **La gracia está fuera de 0 a 365 días:** El campo dice que la gracia va de 0 a 365 días.

## Paso 3. Guarda la vigencia en el borrador

Con «Crear borrador» la vigencia queda en el borrador v1. Guardar un borrador no cambia ningún vínculo: el flujo todavía no está publicado y ninguna validación lo usa. Para que la vigencia rija, publica la versión.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 3: Guarda la vigencia en el borrador (escritorio)](../../../salida/manual/img/configuracion/flujo-vigencia-del-vinculo/03-guarda-la-vigencia-en-el-borrador-claro.webp)

## Recorridos relacionados

- [Pide secciones de «Mi información» como requisito del flujo](flujo-secciones-de-la-ficha.md)
- [Fija el plazo de cargue, los recordatorios y el reintento](flujo-plazo-reintento-y-recordatorios.md)
