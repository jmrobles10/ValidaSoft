---
id: "configuracion/flujo-etapas-y-areas-en-paralelo"
titulo: "Arma una etapa con dos áreas en paralelo y una automática"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora de Constructora Norte arma un flujo de proveedores con una etapa en la que Compras y Cumplimiento revisan al mismo tiempo. Define los aprobadores y el plazo de Compras, deja Cumplimiento como automática y guarda el flujo como borrador."
pasos: 5
---

# Arma una etapa con dos áreas en paralelo y una automática

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora de Constructora Norte arma un flujo de proveedores con una etapa en la que Compras y Cumplimiento revisan al mismo tiempo. Define los aprobadores y el plazo de Compras, deja Cumplimiento como automática y guarda el flujo como borrador.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Norte S.A.S.» y sus áreas Compras (con Rubén) y Cumplimiento (con Olga).
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Flujos aprobatorios
2. Elige las áreas que revisan la etapa
3. Define aprobadores, plazo y rechazo de cada área
4. Marca un área como automática
5. Guarda el flujo como borrador

## Paso 1. Abre Configuración › Flujos aprobatorios

Un flujo define qué áreas revisan a cada tipo de vínculo, en qué orden y qué documentos se piden. Cada etapa se revisa después de la anterior; las áreas de una misma etapa revisan al mismo tiempo y la etapa termina cuando todas aprueban. El botón «Nuevo flujo» crea uno desde cero.

> **Pantalla:** `/e/{empresa}/flujos`

![Paso 1: Abre Configuración › Flujos aprobatorios (escritorio)](../../../salida/manual/img/configuracion/flujo-etapas-y-areas-en-paralelo/01-abre-configuracion-flujos-aprobatorios-claro.webp)

## Paso 2. Elige las áreas que revisan la etapa

En «Etapa 1» eliges un área y con «Agregar área en paralelo» sumas otra que revisa al mismo tiempo: aquí Compras y Cumplimiento. La etapa termina cuando todas sus áreas aprueban, y la etapa siguiente empieza entonces. Cada área debe ser distinta de las demás áreas del flujo.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 2: Elige las áreas que revisan la etapa (escritorio)](../../../salida/manual/img/configuracion/flujo-etapas-y-areas-en-paralelo/02-elige-las-areas-que-revisan-la-etapa-claro.webp)

**Variantes**

- **Agregas otra área a la misma etapa:** Las dos revisan en paralelo y la etapa termina cuando ambas aprueban.
- **Agregas otra etapa:** Sus áreas solo ven el expediente cuando la etapa anterior terminó.

**Configuración que lo afecta**

- **Áreas de la empresa y sus miembros** (`Configuración › Áreas (/areas)`): Solo se pueden elegir áreas activas; quién decide en cada una sale de sus miembros.

## Paso 3. Define aprobadores, plazo y rechazo de cada área

«Aprobadores» es cuántas personas distintas del área deben aprobar, de 1 a 10. El «SLA (horas)» es el plazo para revisar; si el área lo incumple, el informe de cumplimiento lo marca. «Puede rechazar» deja que el área rechace el expediente en lugar de solo observarlo. Bajo cada área ves sus miembros: si son menos que los aprobadores pedidos, el editor lo marca.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 3: Define aprobadores, plazo y rechazo de cada área (escritorio)](../../../salida/manual/img/configuracion/flujo-etapas-y-areas-en-paralelo/03-define-aprobadores-plazo-y-rechazo-de-cada-area-claro.webp)

**Variantes**

- **Pides más aprobadores que miembros tiene el área:** El editor marca el área y no deja publicar el flujo hasta corregirlo.

## Paso 4. Marca un área como automática

«Automática» hace que el área apruebe sola al empezar la etapa, sin que nadie de ella revise. Úsala solo cuando no haga falta una decisión humana: el expediente sigue su curso cuando Compras apruebe. Con «Automática» el área no pide aprobadores y no puede rechazar.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 4: Marca un área como automática (escritorio)](../../../salida/manual/img/configuracion/flujo-etapas-y-areas-en-paralelo/04-marca-un-area-como-automatica-claro.webp)

**Variantes**

- **Marcas «Automática»:** El área aprueba al empezar la etapa, sin que nadie de ella decida.

## Paso 5. Guarda el flujo como borrador

Con «Crear borrador» el flujo queda guardado como borrador v1: todavía no lo usa ningún expediente. Si una etapa tiene un dato inválido, el editor lo marca con el campo en error. Para usarlo hay que publicarlo; mientras tanto puedes seguir editándolo.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 5: Guarda el flujo como borrador (escritorio)](../../../salida/manual/img/configuracion/flujo-etapas-y-areas-en-paralelo/05-guarda-el-flujo-como-borrador-claro.webp)

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](tipo-de-documento-y-flujo.md)
- [Crea un área, asígnale personas y desactívala](areas-y-miembros.md)
