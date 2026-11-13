---
id: "configuracion/tipo-de-documento-y-flujo"
titulo: "Configura un tipo de documento y publica un flujo que lo pide"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora crea un tipo de documento propio (archivo con fecha de expedición, límites y vigencia con avisos) y luego un flujo aprobatorio para proveedores que lo pide, lo publica y lo deja como el flujo por defecto."
pasos: 14
---

# Configura un tipo de documento y publica un flujo que lo pide

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora crea un tipo de documento propio (archivo con fecha de expedición, límites y vigencia con avisos) y luego un flujo aprobatorio para proveedores que lo pide, lo publica y lo deja como el flujo por defecto.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con una persona como miembro y un flujo de Proveedor ya publicado.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Tipos de documento
2. Identifica el documento
3. Elige cómo lo entrega el proveedor
4. Limita el archivo que se puede subir
5. Define la vigencia y los avisos
6. Crea el tipo y revísalo en la tabla
7. Abre Configuración › Flujos aprobatorios
8. Escribe los datos generales del flujo
9. Elige las áreas que revisan en cada etapa
10. Pide el documento nuevo en el flujo
11. Guarda el flujo como borrador
12. Publica la versión
13. Usa el flujo por defecto para el tipo de vínculo
14. Comprueba el resultado en la lista de flujos

## Paso 1. Abre Configuración › Tipos de documento

Desde el engranaje de Configuración entras a «Tipos de documento». La tabla lista, para todo el grupo, qué recibe cada documento (formato y tamaño o datos de un formulario), cuánto vale y si está activo. Los marcados «de la plataforma» parten de un documento conocido, como el RUT, y se comparten entre empresas. Solo quien puede escribir configuración ve el botón para crear uno nuevo.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 1: Abre Configuración › Tipos de documento (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/01-abre-configuracion-tipos-de-documento-claro.webp)

**Configuración que lo afecta**

- **Tipos de documento** (`Configuración › Tipos de documento (/tipos-documento)`): Es el catálogo de documentos que se pueden pedir en los flujos.

## Paso 2. Identifica el documento

Un panel lateral recoge la configuración. Elige «Ninguno: es un documento propio» o parte de un documento de la plataforma (RUT, Cámara de comercio…) cuyas reglas no puedes relajar. Escribe un código en mayúsculas (no cambia después) y el nombre que verá el proveedor. El ámbito decide si el documento queda en la bóveda del proveedor y se reutiliza con otras empresas (Compartido) o es solo de esta relación.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 2: Identifica el documento (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/02-identifica-el-documento-claro.webp)

**Variantes**

- **Partes de un documento de la plataforma:** El ámbito, el formato y los límites quedan acotados por la plataforma y se comparte la bóveda entre empresas.
- **Es un documento propio:** Defines tú el ámbito, el archivo y la vigencia; si lleva formulario, siempre es «Solo de este vínculo».

## Paso 3. Elige cómo lo entrega el proveedor

El documento puede ser solo un archivo, solo un formulario con datos o las dos cosas en la misma entrega. Con formulario aparece la lista de campos que llenará el proveedor (texto, número, fecha, lista o Sí/No), cada uno con su etiqueta, su clave y si es obligatorio. El botón «Agregar fecha de expedición» crea el campo reservado fecha_expedicion, que es la base para calcular la vigencia.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 3: Elige cómo lo entrega el proveedor (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/03-elige-como-lo-entrega-el-proveedor-claro.webp)

**Variantes**

- **Se entrega como «Archivo»:** El proveedor solo sube el archivo; no hay campos.
- **Se entrega como «Formulario»:** El proveedor llena los datos y no sube archivo; no se piden formatos ni peso.

## Paso 4. Limita el archivo que se puede subir

Marca los formatos permitidos (ninguno marcado equivale a PDF, JPG, PNG o WebP) y fija el peso máximo en MB y las páginas máximas. Vacío significa el límite general de la plataforma. Lo que escribas es lo que le dirá la pantalla de carga al proveedor, por ejemplo «Solo PDF de hasta 5 MB y 10 páginas».

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 4: Limita el archivo que se puede subir (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/04-limita-el-archivo-que-se-puede-subir-claro.webp)

**Variantes**

- **El tipo parte de un documento de la plataforma:** Los formatos y límites no pueden pasar del techo que fija la plataforma; el panel lo indica debajo de cada campo.

## Paso 5. Define la vigencia y los avisos

Indica cuánto tiempo sirve el documento: siempre, un número de días desde su expedición (la fecha que declara el proveedor), un número de días desde que lo entrega o un periodo mínimo. Los días de aviso son los momentos antes del vencimiento en que Validasoft le escribe al proveedor para que lo renueve; los días de gracia son los que sigue valiendo después de vencer mientras lo renueva.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 5: Define la vigencia y los avisos (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/05-define-la-vigencia-y-los-avisos-claro.webp)

**Variantes**

- **La regla es «Siempre (no vence)»:** No hay días ni avisos: el documento no pierde vigencia.
- **La regla es «Días desde que se entrega»:** La vigencia se cuenta desde el día en que el proveedor lo entrega, sin pedir fecha de expedición.

**Configuración que lo afecta**

- **Avisos de vencimiento y plantillas de correo de vencimiento** (`Configuración › Plantillas de correo (/plantillas)`): Los avisos que se envían usan estas plantillas.

## Paso 6. Crea el tipo y revísalo en la tabla

Al pulsar «Crear tipo» el panel se cierra, aparece el aviso de que quedó creado y el nuevo tipo entra a la tabla con su resumen: qué recibe, cuánto vale y que está activo. Mientras falte un dato obligatorio, el botón está deshabilitado y dice qué falta. Para cambiarlo después, haz clic en su fila; un tipo inactivo deja de poder pedirse en los flujos nuevos.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 6: Crea el tipo y revísalo en la tabla (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/06-crea-el-tipo-y-revisalo-en-la-tabla-claro.webp)

## Paso 7. Abre Configuración › Flujos aprobatorios

Un flujo define qué áreas revisan a cada tipo de vínculo, en qué orden y qué documentos se piden. La tabla muestra cada flujo con su tipo de vínculo, su versión publicada, si hay un borrador y cuántas etapas tiene; el que usa la empresa cuando invita a un tercero de ese tipo lleva la marca «Por defecto». Sin un flujo publicado, los terceros que aceptan la invitación quedan en «aperturas pendientes».

> **Pantalla:** `/e/{empresa}/flujos`

![Paso 7: Abre Configuración › Flujos aprobatorios (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/07-abre-configuracion-flujos-aprobatorios-claro.webp)

**Configuración que lo afecta**

- **Flujos aprobatorios** (`Configuración › Flujos aprobatorios (/flujos)`): Cada empresa del grupo tiene sus propios flujos.

## Paso 8. Escribe los datos generales del flujo

Un flujo nuevo empieza con su código (mayúsculas, números y guion bajo; no cambia después), su nombre y el tipo de vínculo al que aplica (tampoco cambia después). Más abajo se configura qué pasa cuando un área observa, la separación de funciones, el plazo de cargue, los recordatorios, el reintento tras un rechazo y la vigencia periódica del vínculo.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 8: Escribe los datos generales del flujo (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/08-escribe-los-datos-generales-del-flujo-claro.webp)

**Variantes**

- **Si un área observa: «Devolver de inmediato»:** La primera observación devuelve el expediente al tercero y frena a las demás áreas.
- **Si un área observa: «Esperar a que la etapa se pronuncie»:** Las demás áreas siguen revisando y el expediente se devuelve cuando todas se pronunciaron.
- **Activas la separación de funciones:** Quien invitó al tercero o abrió el expediente no puede decidir sobre él.

## Paso 9. Elige las áreas que revisan en cada etapa

Las etapas se revisan en secuencia; las áreas de una misma etapa revisan al mismo tiempo y la etapa siguiente empieza cuando todas aprueban. Para cada área eliges cuántos aprobadores distintos necesita (de 1 a 10), un SLA opcional en horas, si puede rechazar el expediente y si es automática (aprueba sola al empezar la etapa). Bajo el área ves cuántos miembros tiene: si son menos que los aprobadores pedidos, el editor lo marca porque la etapa nunca se completaría.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 9: Elige las áreas que revisan en cada etapa (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/09-elige-las-areas-que-revisan-en-cada-etapa-claro.webp)

**Variantes**

- **Agregas otra área a la misma etapa:** Las dos revisan en paralelo y la etapa termina cuando ambas aprueban.
- **Agregas otra etapa:** Sus áreas solo ven el expediente cuando la etapa anterior terminó.

**Configuración que lo afecta**

- **Áreas de la empresa y sus miembros** (`Configuración › Áreas (/areas)`): Solo se pueden elegir áreas activas; quién decide en cada una sale de sus miembros.

## Paso 10. Pide el documento nuevo en el flujo

«Documentos que pide» lista lo que el tercero debe cargar. Al agregar uno eliges su tipo (el «Certificado de trabajo en alturas» recién creado ya aparece, con su resumen), qué áreas lo revisan, a quién se le pide, cuántos aprobadores necesita y si es obligatorio. Toda área debe revisar al menos un documento; sin documentos, cada área aprueba u observa el expediente completo.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 10: Pide el documento nuevo en el flujo (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/10-pide-el-documento-nuevo-en-el-flujo-claro.webp)

**Variantes**

- **Se pide a «Solo personas jurídicas» o «Solo personas naturales»:** El documento solo aparece en la lista del tercero de ese tipo de persona.
- **Agregas una sección de la información:** El tercero la diligencia en «Mi información» y la empresa la revisa como un requisito más.

**Configuración que lo afecta**

- **Tipos de documento** (`Configuración › Tipos de documento (/tipos-documento)`): Solo se pueden pedir tipos activos que no sean solo formulario.

## Paso 11. Guarda el flujo como borrador

Con «Crear borrador» el flujo queda guardado como borrador v1: todavía no lo usa ningún expediente. Si falta algo, el editor marca los campos con error y dice qué corregir. Mientras haya cambios sin guardar no puedes publicar; cada cambio que guardes sobre una versión ya publicada crea un borrador nuevo y la versión vigente sigue funcionando hasta que publiques.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 11: Guarda el flujo como borrador (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/11-guarda-el-flujo-como-borrador-claro.webp)

## Paso 12. Publica la versión

Antes de publicar, Validasoft te avisa qué va a pasar: los expedientes nuevos usarán esta versión, los que ya están abiertos siguen con la suya y la versión publicada anterior queda retirada. Al confirmar, la versión queda congelada y se registra en la auditoría de configuración.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 12: Publica la versión (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/12-publica-la-version-claro.webp)

## Paso 13. Usa el flujo por defecto para el tipo de vínculo

Publicar no cambia a qué flujo va cada invitación: eso lo decide el flujo por defecto de cada tipo de vínculo. Mientras no marques este, las invitaciones a proveedores siguen usando el anterior. «Usar por defecto para Proveedor» solo aparece en flujos publicados del mismo tipo.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 13: Usa el flujo por defecto para el tipo de vínculo (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/13-usa-el-flujo-por-defecto-para-el-tipo-de-vinculo-claro.webp)

## Paso 14. Comprueba el resultado en la lista de flujos

La lista muestra el flujo nuevo con su versión publicada (v1) y la marca «Por defecto». Desde ahora, cuando el sistema integrado invite a un proveedor de Constructora Sur, el tercero recibirá una revisión con el documento nuevo; las revisiones ya abiertas no cambian. Para modificar el flujo se abre su fila y se guarda un borrador nuevo.

> **Pantalla:** `/e/{empresa}/flujos`

![Paso 14: Comprueba el resultado en la lista de flujos (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-y-flujo/14-comprueba-el-resultado-en-la-lista-de-flujos-claro.webp)

**Variantes**

- **La empresa no tiene un flujo por defecto para el tipo de vínculo:** La lista de «Flujo por defecto por tipo de vínculo» dice «Sin flujo por defecto» y los terceros que aceptan quedan en aperturas pendientes.

## Recorridos relacionados

- [Acepta la invitación y completa tu información como persona jurídica](../tercero/aceptar-invitacion-persona-juridica.md)
