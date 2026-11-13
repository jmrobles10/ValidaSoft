---
id: "configuracion/flujo-condiciones-por-tipo-de-persona"
titulo: "Pide cada documento según el tipo de persona"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora arma un flujo de proveedores en el que el RUT se pide a todos, la Cámara de comercio solo a personas jurídicas y la cédula solo a personas naturales. Así el tercero entrega lo que le corresponde y nada más."
pasos: 3
---

# Pide cada documento según el tipo de persona

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora arma un flujo de proveedores en el que el RUT se pide a todos, la Cámara de comercio solo a personas jurídicas y la cédula solo a personas naturales. Así el tercero entrega lo que le corresponde y nada más.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con miembros y los tipos de documento RUT, Cámara de comercio y Cédula.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre un flujo nuevo para proveedores
2. Elige a quién se pide cada documento
3. Guarda el flujo como borrador

## Paso 1. Abre un flujo nuevo para proveedores

Cada documento de un flujo tiene una columna «Se pide a» que decide a qué terceros se les pide. Por omisión es «Todos»; las otras dos opciones limitan el documento al tipo de persona del tercero: solo personas jurídicas o solo personas naturales. Para armar el ejemplo creamos un flujo nuevo de proveedores desde «Nuevo flujo».

> **Pantalla:** `/e/{empresa}/flujos`

![Paso 1: Abre un flujo nuevo para proveedores (escritorio)](../../../salida/manual/img/configuracion/flujo-condiciones-por-tipo-de-persona/01-abre-un-flujo-nuevo-para-proveedores-claro.webp)

**Configuración que lo afecta**

- **Se pide a (por documento del flujo)** (`Configuración › Flujos aprobatorios (/flujos)`): Un documento que no aplica al tercero no aparece en su lista y el área que solo lo revisa no tiene nada que decidir.

## Paso 2. Elige a quién se pide cada documento

El RUT queda en «Todos»: lo entregan las personas jurídicas y las naturales. La Cámara de comercio queda en «Solo personas jurídicas» y la cédula en «Solo personas naturales». Cada documento también elige qué áreas lo revisan: por omisión, «Todas las áreas del flujo»; como este flujo tiene una sola área, Revisión revisa los tres. «Aprobadores» dice cuántas personas deben aprobar, y «Los del área» usa a todos los miembros. Un documento que no le toca a un tercero no lo ve ni lo entrega.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 2: Elige a quién se pide cada documento (escritorio)](../../../salida/manual/img/configuracion/flujo-condiciones-por-tipo-de-persona/02-elige-a-quien-se-pide-cada-documento-claro.webp)

**Variantes**

- **Eliges «Solo personas jurídicas»:** El documento aparece en la lista del tercero solo si es persona jurídica.
- **El documento tiene una condición que no coincide con ningún atajo:** Aparece la opción «Condición personalizada» y dice en qué se pide, por ejemplo «Se pide cuando …».

## Paso 3. Guarda el flujo como borrador

Con «Crear borrador» las condiciones quedan guardadas en el borrador v1. Todavía no se usan: el flujo se publica después, igual que cualquier otro, y solo entonces los terceros verán cada documento según su tipo de persona.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 3: Guarda el flujo como borrador (escritorio)](../../../salida/manual/img/configuracion/flujo-condiciones-por-tipo-de-persona/03-guarda-el-flujo-como-borrador-claro.webp)

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](tipo-de-documento-y-flujo.md)
