---
id: "tercero/aceptar-invitacion-persona-natural"
titulo: "Acepta la invitación y completa tu información como persona natural"
actor: "Tercero"
dominio: "tercero"
resumen: "Una empresa invita a una persona natural como cliente. Recibe el correo, acepta los textos legales y ve que le piden el RUT y la cédula, y cuatro secciones de «Mi información» (sin Legal ni Financiera). Completa cada sección y sube sus documentos hasta que su expediente pasa a revisión."
pasos: 12
---

# Acepta la invitación y completa tu información como persona natural

**Actor:** Tercero · **Dominio:** Portal del tercero

Una empresa invita a una persona natural como cliente. Recibe el correo, acepta los textos legales y ve que le piden el RUT y la cédula, y cuatro secciones de «Mi información» (sin Legal ni Financiera). Completa cada sección y sube sus documentos hasta que su expediente pasa a revisión.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Cliente publicado que pide, según el tipo de persona, el RUT, la cédula (solo a personas naturales), la Cámara de comercio (solo a personas jurídicas) y las seis secciones de la ficha.
- Una persona natural invitada como cliente por el sistema integrado (el correo de invitación ya llegó).

**Pasos**

1. Abre la invitación que llega a tu correo
2. Lee y acepta los textos legales
3. Revisa lo que te pide la empresa
4. Entra a «Mi información»
5. Verifica que las secciones de persona jurídica no te aplican
6. Diligencia tu información general
7. Completa la información tributaria
8. Responde seguridad y salud en el trabajo
9. Elige tus especialidades
10. Confirma que tu información está completa
11. Sube tu cédula de ciudadanía
12. Espera la revisión de la empresa

## Paso 1. Abre la invitación que llega a tu correo

La empresa te escribe desde Validasoft con el asunto «Constructora Sur S.A.S. te invita a validar tu vínculo como cliente». El correo dice quién te invita y trae el enlace «Abrir Validasoft». El enlace es personal, de un solo uso y tiene fecha de vencimiento; si se vence, la empresa puede enviarte otra invitación.

![Paso 1: Abre la invitación que llega a tu correo (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/01-abre-la-invitacion-que-llega-a-tu-correo-claro.webp)

![Paso 1: Abre la invitación que llega a tu correo (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/01-abre-la-invitacion-que-llega-a-tu-correo-celular.webp)

**Variantes**

- **El tipo de vínculo es Proveedor:** Antes de abrir la invitación debes escribir un código de 6 dígitos que llega a este mismo correo.
- **El tipo de vínculo es Cliente:** El enlace abre la invitación directamente, sin código.

**Configuración que lo afecta**

- **Exige un código por correo en el enlace** (`Configuración › Tipos de vínculo (/tipos-vinculo)`): Según el tipo de vínculo, el enlace pide o no el código de 6 dígitos.

## Paso 2. Lee y acepta los textos legales

Como cliente, el enlace te lleva directo a la invitación: no escribes ningún código. Ves quién te invita y los textos que debes aceptar, el aviso de tratamiento de datos de Validasoft y los que la empresa publicó. Marca «Leí y acepto este texto» en cada uno y pulsa «Aceptar». Al aceptar, Validasoft guarda la versión de cada texto y la fecha como constancia.

> **Pantalla:** `/empresas/{id}`

![Paso 2: Lee y acepta los textos legales (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/02-lee-y-acepta-los-textos-legales-claro.webp)

![Paso 2: Lee y acepta los textos legales (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/02-lee-y-acepta-los-textos-legales-celular.webp)

**Variantes**

- **La empresa aún no publicó su autorización y sus términos:** No puedes aceptar: la pantalla dice que la empresa aún no publica sus textos y que te avisarán.

**Configuración que lo afecta**

- **Textos legales de la empresa** (`Configuración › Empresas del grupo (/empresas)`): Definen el contenido de la autorización y de los términos que aceptas.

## Paso 3. Revisa lo que te pide la empresa

En «Tu revisión con …» la frase resume tu estado («Te faltan documentos y secciones») y la fecha límite. «Lo que te pide» lista lo que te toca: como persona natural te piden el RUT y la cédula de ciudadanía, y cuatro secciones de tu información. No te piden la Cámara de comercio: ese documento es solo para personas jurídicas.

> **Pantalla:** `/expediente/{id}`

![Paso 3: Revisa lo que te pide la empresa (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/03-revisa-lo-que-te-pide-la-empresa-claro.webp)

![Paso 3: Revisa lo que te pide la empresa (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/03-revisa-lo-que-te-pide-la-empresa-celular.webp)

**Variantes**

- **Eres persona jurídica:** En lugar de la cédula te pide la Cámara de comercio, y tu información tiene las seis secciones.

**Configuración que lo afecta**

- **Requisitos del flujo y condiciones «se pide cuando»** (`Configuración › Flujos aprobatorios (/flujos)`): Cada documento puede pedirse solo a un tipo de persona: así la cédula es solo para naturales.

## Paso 4. Entra a «Mi información»

«Mi información» es tu ficha, una sola para todas las empresas con las que te relacionas. Como persona natural tiene cuatro secciones: General, Tributaria, Seguridad y salud en el trabajo, y Especialidades. Arriba ves cuántas llevas completas («0 de 4 secciones completas»).

> **Pantalla:** `/ficha`

![Paso 4: Entra a «Mi información» (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/04-entra-a-mi-informacion-claro.webp)

![Paso 4: Entra a «Mi información» (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/04-entra-a-mi-informacion-celular.webp)

## Paso 5. Verifica que las secciones de persona jurídica no te aplican

La información legal (socios, junta directiva, revisor fiscal) y la financiera (balances) solo se piden a personas jurídicas. Si entras a una de ellas por la dirección, la pantalla dice «Esta sección no aplica para ti» y no tienes nada que diligenciar.

> **Pantalla:** `/ficha/legal`

![Paso 5: Verifica que las secciones de persona jurídica no te aplican (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/05-verifica-que-las-secciones-de-persona-juridica-n-claro.webp)

![Paso 5: Verifica que las secciones de persona jurídica no te aplican (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/05-verifica-que-las-secciones-de-persona-juridica-n-celular.webp)

**Variantes**

- **Eres persona jurídica:** Ambas secciones aparecen en tu lista y debes diligenciarlas.

## Paso 6. Diligencia tu información general

Tus nombres y apellidos llegan prellenados con lo que la empresa registró al invitarte; revísalos. En lugar de la razón social, pide la fecha y el lugar de expedición de tu documento. Completa también el teléfono, el domicilio y tu actividad económica, y responde las preguntas de persona expuesta políticamente (PEP): si lo eres, o si tienes vínculo con una, Validasoft te pedirá los datos. Mientras falte un dato, «Guardar y continuar» queda deshabilitado.

> **Pantalla:** `/ficha/general`

![Paso 6: Diligencia tu información general (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/06-diligencia-tu-informacion-general-claro.webp)

![Paso 6: Diligencia tu información general (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/06-diligencia-tu-informacion-general-celular.webp)

**Variantes**

- **Eres persona jurídica:** En vez de nombres y apellidos ves la razón social, y no hay preguntas de PEP propias.

## Paso 7. Completa la información tributaria

Elige las responsabilidades de la casilla 53 de tu RUT y responde las preguntas sobre ICA y CREE. Al guardar, la sección queda completa y el avance de «Mi información» sube.

> **Pantalla:** `/ficha/tributaria`

![Paso 7: Completa la información tributaria (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/07-completa-la-informacion-tributaria-claro.webp)

![Paso 7: Completa la información tributaria (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/07-completa-la-informacion-tributaria-celular.webp)

## Paso 8. Responde seguridad y salud en el trabajo

Son preguntas de Sí o No sobre tu sistema de gestión de seguridad y salud en el trabajo. Responde todas para que la sección quede completa.

> **Pantalla:** `/ficha/sst`

![Paso 8: Responde seguridad y salud en el trabajo (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/08-responde-seguridad-y-salud-en-el-trabajo-claro.webp)

![Paso 8: Responde seguridad y salud en el trabajo (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/08-responde-seguridad-y-salud-en-el-trabajo-celular.webp)

## Paso 9. Elige tus especialidades

Busca por nombre y marca las especialidades que ofreces; quedan resumidas en «Tus especialidades». La empresa las usa para saber en qué puede contratarte.

> **Pantalla:** `/ficha/especialidades`

![Paso 9: Elige tus especialidades (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/09-elige-tus-especialidades-claro.webp)

![Paso 9: Elige tus especialidades (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/09-elige-tus-especialidades-celular.webp)

## Paso 10. Confirma que tu información está completa

Con las cuatro secciones guardadas, «Mi información» muestra «Tu información está completa» y «4 de 4 secciones completas». Desde aquí puedes volver a cualquier sección para corregirla cuando cambie algo.

> **Pantalla:** `/ficha`

![Paso 10: Confirma que tu información está completa (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/10-confirma-que-tu-informacion-esta-completa-claro.webp)

![Paso 10: Confirma que tu información está completa (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/10-confirma-que-tu-informacion-esta-completa-celular.webp)

## Paso 11. Sube tu cédula de ciudadanía

Cada documento tiene su pantalla de carga con sus reglas: formatos permitidos, tamaño máximo y, si aplica, los datos que debes escribir. Pulsa «Elegir archivo» y selecciona el PDF de tu cédula; Validasoft lo guarda en tu bóveda y te confirma la versión.

> **Pantalla:** `/boveda/subir`

![Paso 11: Sube tu cédula de ciudadanía (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/11-sube-tu-cedula-de-ciudadania-claro.webp)

![Paso 11: Sube tu cédula de ciudadanía (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/11-sube-tu-cedula-de-ciudadania-celular.webp)

**Configuración que lo afecta**

- **Formatos, tamaño, campos y vigencia del tipo de documento** (`Configuración › Tipos de documento (/tipos-documento)`)

## Paso 12. Espera la revisión de la empresa

Cuando no falta ningún documento ni sección, el expediente pasa solo a revisión: no hay un botón «Enviar». La frase de arriba dice que la empresa está revisando tu información y que te avisa por correo si necesita algo. La empresa recibe exactamente lo que se pide a una persona natural: el RUT, la cédula y las cuatro secciones.

> **Pantalla:** `/expediente/{id}`

![Paso 12: Espera la revisión de la empresa (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/12-espera-la-revision-de-la-empresa-claro.webp)

![Paso 12: Espera la revisión de la empresa (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-natural/12-espera-la-revision-de-la-empresa-celular.webp)

**Variantes**

- **La empresa observa un documento:** Recibes un correo con los motivos y la frase cambia a «te pide corregir lo siguiente».
- **La empresa aprueba:** La frase cambia a «aprobó tu información. Tu vínculo está activo».

## Recorridos relacionados

- [Acepta la invitación y completa tu información como persona jurídica](aceptar-invitacion-persona-juridica.md)
