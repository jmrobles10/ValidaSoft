---
id: "configuracion/flujo-por-defecto"
titulo: "Cambia el flujo por defecto de un tipo de vínculo"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora de Constructora Sur tiene un flujo de proveedores ya en uso y publica otro para el mismo tipo. Lo deja como flujo por defecto: desde ahora las invitaciones a proveedores usan el nuevo."
pasos: 3
---

# Cambia el flujo por defecto de un tipo de vínculo

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora de Constructora Sur tiene un flujo de proveedores ya en uso y publica otro para el mismo tipo. Lo deja como flujo por defecto: desde ahora las invitaciones a proveedores usan el nuevo.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» y un flujo de Proveedor ya publicado como por defecto.
- Un segundo flujo de Proveedor publicado (se siembra por la API del sistema de pruebas, sin documentos).
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Revisa qué flujo usa cada tipo de vínculo
2. Marca el flujo como el nuevo por defecto
3. Comprueba el resultado en la tarjeta

## Paso 1. Revisa qué flujo usa cada tipo de vínculo

Cuando una invitación no trae un flujo propio, la empresa usa el flujo por defecto de su tipo de vínculo. La tarjeta «Flujo por defecto por tipo de vínculo» muestra cuál es para cada tipo; en la tabla, el flujo por defecto lleva la marca «Por defecto». Publicar un flujo nuevo no lo cambia: solo los flujos publicados del mismo tipo pueden ser por defecto.

> **Pantalla:** `/e/{empresa}/flujos`

![Paso 1: Revisa qué flujo usa cada tipo de vínculo (escritorio)](../../../salida/manual/img/configuracion/flujo-por-defecto/01-revisa-que-flujo-usa-cada-tipo-de-vinculo-claro.webp)

**Configuración que lo afecta**

- **Flujo por defecto por tipo de vínculo** (`Configuración › Flujos aprobatorios (/flujos)`): Las invitaciones nuevas de ese tipo usan este flujo, salvo que traigan uno propio.

## Paso 2. Marca el flujo como el nuevo por defecto

Dentro del flujo, «Usar por defecto para Proveedor» lo convierte en el flujo por defecto de ese tipo de vínculo. El botón solo aparece en flujos publicados del mismo tipo. El flujo anterior deja de ser el por defecto, pero sigue vigente para las revisiones que ya están abiertas.

> **Pantalla:** `/e/{empresa}/flujos`

![Paso 2: Marca el flujo como el nuevo por defecto (escritorio)](../../../salida/manual/img/configuracion/flujo-por-defecto/02-marca-el-flujo-como-el-nuevo-por-defecto-claro.webp)

**Variantes**

- **El flujo es un borrador sin publicar:** El botón no aparece: primero hay que publicar el flujo.

## Paso 3. Comprueba el resultado en la tarjeta

La tarjeta de flujo por defecto ahora muestra el flujo nuevo para Proveedor. Si una empresa no tiene flujo por defecto para un tipo, la tarjeta dice «Sin flujo por defecto» y los terceros que aceptan quedan en aperturas pendientes.

> **Pantalla:** `/e/{empresa}/flujos`

![Paso 3: Comprueba el resultado en la tarjeta (escritorio)](../../../salida/manual/img/configuracion/flujo-por-defecto/03-comprueba-el-resultado-en-la-tarjeta-claro.webp)

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](tipo-de-documento-y-flujo.md)
- [Arma una etapa con dos áreas en paralelo y una automática](flujo-etapas-y-areas-en-paralelo.md)
