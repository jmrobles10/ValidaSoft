---
id: "tercero/aceptar-invitacion-persona-juridica"
titulo: "Acepta la invitación y completa tu información como persona jurídica"
actor: "Tercero"
dominio: "tercero"
resumen: "Una empresa invita a un proveedor persona jurídica. Recibe el correo, entra con un código, acepta los textos legales, diligencia las seis secciones de «Mi información» y sube sus documentos hasta que su expediente pasa a revisión."
pasos: 17
---

# Acepta la invitación y completa tu información como persona jurídica

**Actor:** Tercero · **Dominio:** Portal del tercero

Una empresa invita a un proveedor persona jurídica. Recibe el correo, entra con un código, acepta los textos legales, diligencia las seis secciones de «Mi información» y sube sus documentos hasta que su expediente pasa a revisión.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Proveedor publicado como flujo por defecto que pide RUT, Cámara de comercio y las seis secciones de la ficha.
- Un tercero persona jurídica invitado por el sistema integrado (el correo de invitación ya llegó).

**Pasos**

1. Abre la invitación que llega a tu correo
2. Escribe el código de acceso
3. Lee y acepta los textos legales
4. Revisa lo que te pide la empresa
5. Entra a «Mi información»
6. Diligencia la información general
7. Completa la información tributaria
8. Agrega a tu representante legal y a tus socios
9. Guarda la información legal
10. Registra la información financiera
11. Responde seguridad y salud en el trabajo
12. Elige tus especialidades
13. Confirma que tu información está completa
14. Vuelve a tu revisión con la empresa
15. Elige el archivo del documento
16. Confirma la versión que quedó guardada
17. Espera la revisión de la empresa

## Paso 1. Abre la invitación que llega a tu correo

La empresa te escribe desde Validasoft con el asunto «Constructora Sur S.A.S. te invita a validar tu vínculo como proveedor». El correo dice quién te invita y trae el enlace «Abrir Validasoft». El enlace es personal, de un solo uso y tiene fecha de vencimiento; si lo pierdes o se vence, la empresa puede enviarte otra invitación.

![Paso 1: Abre la invitación que llega a tu correo (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/01-abre-la-invitacion-que-llega-a-tu-correo-claro.webp)

![Paso 1: Abre la invitación que llega a tu correo (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/01-abre-la-invitacion-que-llega-a-tu-correo-celular.webp)

**Variantes**

- **La empresa configuró un remitente propio:** El correo sale con el dominio de la empresa y las respuestas llegan a su buzón.

**Configuración que lo afecta**

- **Plantillas de correo y Dominio de correo de la empresa** (`Configuración › Plantillas de correo y Dominio de correo (/plantillas, /dominio-correo)`)

## Paso 2. Escribe el código de acceso

Como el tipo de vínculo «Proveedor» exige verificar el correo, Validasoft envía un código de 6 dígitos a la misma dirección de la invitación. Escríbelo en los seis recuadros y pulsa «Continuar». Si no llega, revisa la carpeta de spam; si el código se venció, abre de nuevo el enlace del correo para recibir otro. No necesitas crear contraseña.

> **Pantalla:** `/e/{enlace}`

![Paso 2: Escribe el código de acceso (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/02-escribe-el-codigo-de-acceso-claro.webp)

![Paso 2: Escribe el código de acceso (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/02-escribe-el-codigo-de-acceso-celular.webp)

**Variantes**

- **El tipo de vínculo no exige código (por ejemplo, Cliente):** Este paso no aparece: el enlace del correo abre directo la invitación.

**Configuración que lo afecta**

- **Exige un código por correo en el enlace** (`Configuración › Tipos de vínculo (/tipos-vinculo)`): Activa o quita el código de 6 dígitos al abrir el enlace.

## Paso 3. Lee y acepta los textos legales

Antes de entregar nada ves quién te invita y los textos que debes aceptar: el aviso de tratamiento de datos de Validasoft y los que la empresa publicó (su autorización y sus términos y condiciones). Cada texto muestra su versión y la fecha en que se publicó. Marca «Leí y acepto este texto» en cada uno; si pulsas «Aceptar» con alguno sin marcar, la pantalla te pide marcar cada texto para continuar. Al aceptar, Validasoft guarda la versión de cada texto, la fecha y tu conexión como constancia, y desde ese momento tus documentos se comparten con la empresa.

> **Pantalla:** `/empresas/{id}`

![Paso 3: Lee y acepta los textos legales (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/03-lee-y-acepta-los-textos-legales-claro.webp)

![Paso 3: Lee y acepta los textos legales (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/03-lee-y-acepta-los-textos-legales-celular.webp)

**Variantes**

- **La empresa aún no publicó su autorización y sus términos:** No puedes aceptar: la pantalla dice que la empresa aún no publica sus textos y que te avisarán.
- **Ya habías aceptado esta invitación:** La pantalla dice «Ya aceptaste esta invitación» con la fecha, y puedes ir a tu revisión.

**Configuración que lo afecta**

- **Textos legales de la empresa** (`Configuración › Empresas del grupo (/empresas)`): Definen el contenido de la autorización y de los términos que aceptas.

## Paso 4. Revisa lo que te pide la empresa

En «Tu revisión con …» una frase (1) resume el estado («Te faltan 8 documentos» cuenta los documentos y las secciones de tu información) y la fecha límite para completarlo. Debajo, «Lo que te pide» separa lo que te toca (2: documentos por subir y secciones por diligenciar) de lo que ya está en manos de la empresa («Lo revisa …»). Cada requisito muestra su estado y, en los documentos, el formato y el tamaño permitidos.

> **Pantalla:** `/expediente/{id}`

![Paso 4: Revisa lo que te pide la empresa (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/04-revisa-lo-que-te-pide-la-empresa-claro.webp)

![Paso 4: Revisa lo que te pide la empresa (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/04-revisa-lo-que-te-pide-la-empresa-celular.webp)

**Variantes**

- **Eres persona natural:** En lugar de la Cámara de comercio te pide la cédula, y tu información tiene cuatro secciones (sin Legal ni Financiera).
- **Eres persona jurídica:** Te pide RUT, Cámara de comercio y las seis secciones de tu información.

**Configuración que lo afecta**

- **Requisitos del flujo y condiciones «se pide cuando»** (`Configuración › Flujos aprobatorios (/flujos)`): Definen qué documentos y secciones se le piden a cada tipo de persona.
- **Plazo de cargue** (`Configuración › Flujos aprobatorios (/flujos)`): Fija hasta cuándo puedes completar el expediente.

## Paso 5. Entra a «Mi información»

«Mi información» es tu ficha: una sola para todas las empresas con las que te relacionas. Se divide en secciones y arriba ves cuántas llevas completas. Puedes entrar en el orden que quieras y volver después; lo que diligencies aquí se comparte con las empresas que te lo piden.

> **Pantalla:** `/ficha`

![Paso 5: Entra a «Mi información» (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/05-entra-a-mi-informacion-claro.webp)

![Paso 5: Entra a «Mi información» (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/05-entra-a-mi-informacion-celular.webp)

## Paso 6. Diligencia la información general

La identificación, el tipo de persona, la razón social y el correo llegan prellenados con lo que la empresa registró al invitarte; revísalos. Completa teléfono, país, ciudad, dirección y tu actividad económica (CIIU). La barra de abajo dice si la sección ya está lista; mientras falte un dato, «Guardar y continuar» queda deshabilitado y te indica cuántos faltan.

> **Pantalla:** `/ficha/general`

![Paso 6: Diligencia la información general (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/06-diligencia-la-informacion-general-claro.webp)

![Paso 6: Diligencia la información general (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/06-diligencia-la-informacion-general-celular.webp)

**Variantes**

- **Eres persona natural:** En vez de razón social ves nombres y apellidos, la fecha y el lugar de expedición del documento y las preguntas de persona expuesta políticamente (PEP).

## Paso 7. Completa la información tributaria

Elige las responsabilidades de la casilla 53 de tu RUT y responde las preguntas sobre ICA y CREE. Al guardar, la sección queda marcada como completa y el avance de «Mi información» sube.

> **Pantalla:** `/ficha/tributaria`

![Paso 7: Completa la información tributaria (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/07-completa-la-informacion-tributaria-claro.webp)

![Paso 7: Completa la información tributaria (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/07-completa-la-informacion-tributaria-celular.webp)

## Paso 8. Agrega a tu representante legal y a tus socios

La información legal pide las personas que están detrás de la empresa: representante legal, socios o accionistas, junta directiva, revisor fiscal y contador, como en tu Cámara de comercio. En «Agregar persona» eliges el rol, indicas el documento y el nombre y declaras si es persona expuesta políticamente (PEP) o tiene vínculo con una. Para un socio también va su participación (%).

> **Pantalla:** `/ficha/legal`

![Paso 8: Agrega a tu representante legal y a tus socios (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/08-agrega-a-tu-representante-legal-y-a-tus-socios-claro.webp)

![Paso 8: Agrega a tu representante legal y a tus socios (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/08-agrega-a-tu-representante-legal-y-a-tus-socios-celular.webp)

**Variantes**

- **Agregas un socio o accionista:** Además del documento y el nombre, indicas su porcentaje de participación.

## Paso 9. Guarda la información legal

La sección no queda completa solo con el representante legal: la empresa exige al menos un socio o accionista. Con los dos en la lista, «Guardar» confirma la sección y el avance de «Mi información» sube a tres de seis.

> **Pantalla:** `/ficha/legal`

![Paso 9: Guarda la información legal (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/09-guarda-la-informacion-legal-claro.webp)

![Paso 9: Guarda la información legal (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/09-guarda-la-informacion-legal-celular.webp)

**Variantes**

- **Solo registras al representante legal:** La barra dice «Falta 1 dato para continuar» y un aviso pide registrar al menos un socio o accionista.

## Paso 10. Registra la información financiera

Indica la fecha de corte del último cierre y las cifras de activos, pasivos, patrimonio, ingresos y gastos. Validasoft comprueba que activos = pasivos + patrimonio y te avisa si no cuadra. El título de la sección lleva el año del periodo.

> **Pantalla:** `/ficha/financiera`

![Paso 10: Registra la información financiera (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/10-registra-la-informacion-financiera-claro.webp)

![Paso 10: Registra la información financiera (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/10-registra-la-informacion-financiera-celular.webp)

## Paso 11. Responde seguridad y salud en el trabajo

Son preguntas de Sí o No sobre tu sistema de gestión de seguridad y salud en el trabajo. Responde todas para que la sección quede completa.

> **Pantalla:** `/ficha/sst`

![Paso 11: Responde seguridad y salud en el trabajo (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/11-responde-seguridad-y-salud-en-el-trabajo-claro.webp)

![Paso 11: Responde seguridad y salud en el trabajo (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/11-responde-seguridad-y-salud-en-el-trabajo-celular.webp)

## Paso 12. Elige tus especialidades

Busca por nombre y marca las especialidades que ofreces; quedan resumidas en «Tus especialidades». La empresa las usa para saber en qué puede contratarte.

> **Pantalla:** `/ficha/especialidades`

![Paso 12: Elige tus especialidades (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/12-elige-tus-especialidades-claro.webp)

![Paso 12: Elige tus especialidades (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/12-elige-tus-especialidades-celular.webp)

## Paso 13. Confirma que tu información está completa

Con las seis secciones guardadas, «Mi información» muestra «Tu información está completa». Desde aquí puedes volver a cualquier sección para corregirla cuando cambie algo de tu empresa.

> **Pantalla:** `/ficha`

![Paso 13: Confirma que tu información está completa (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/13-confirma-que-tu-informacion-esta-completa-claro.webp)

![Paso 13: Confirma que tu información está completa (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/13-confirma-que-tu-informacion-esta-completa-celular.webp)

## Paso 14. Vuelve a tu revisión con la empresa

«Mis empresas» lista todas las empresas que te invitaron, cada una con su tipo de vínculo (Proveedor, Cliente…). Abre la tarjeta de Constructora Sur para ver qué documentos te faltan.

> **Pantalla:** `/empresas`

![Paso 14: Vuelve a tu revisión con la empresa (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/14-vuelve-a-tu-revision-con-la-empresa-claro.webp)

![Paso 14: Vuelve a tu revisión con la empresa (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/14-vuelve-a-tu-revision-con-la-empresa-celular.webp)

## Paso 15. Elige el archivo del documento

Cada documento tiene su propia pantalla de carga con las reglas del tipo: formatos permitidos, tamaño máximo y, si aplica, los datos que debes escribir (por ejemplo, la fecha de expedición). Pulsa «Elegir archivo» y selecciona el PDF (también puedes arrastrarlo a la pantalla); Validasoft lo guarda en tu bóveda.

> **Pantalla:** `/boveda/subir`

![Paso 15: Elige el archivo del documento (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/15-elige-el-archivo-del-documento-claro.webp)

![Paso 15: Elige el archivo del documento (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/15-elige-el-archivo-del-documento-celular.webp)

**Configuración que lo afecta**

- **Formatos, tamaño, campos y vigencia del tipo de documento** (`Configuración › Tipos de documento (/tipos-documento)`)

## Paso 16. Confirma la versión que quedó guardada

Validasoft confirma «Listo. Es tu versión 1» del documento y dice qué empresa ya la tiene. Si más adelante subes otro archivo del mismo documento, quedará como versión 2 y las empresas que lo usan vuelven a revisarlo. Pulsa «Volver a mi revisión» para seguir con el siguiente documento; también puedes ver tu bóveda o elegir otro archivo.

> **Pantalla:** `/boveda/subir`

![Paso 16: Confirma la versión que quedó guardada (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/16-confirma-la-version-que-quedo-guardada-claro.webp)

![Paso 16: Confirma la versión que quedó guardada (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/16-confirma-la-version-que-quedo-guardada-celular.webp)

## Paso 17. Espera la revisión de la empresa

Cuando no falta ningún documento ni sección, el expediente pasa solo a revisión: no hay un botón «Enviar». La frase de arriba dice que la empresa está revisando tu información y que te avisa por correo si necesita algo. Cada documento muestra su vigencia («Vigente», «Por vencer») y cuándo vence, y te deja subir otra versión.

> **Pantalla:** `/expediente/{id}`

![Paso 17: Espera la revisión de la empresa (escritorio)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/17-espera-la-revision-de-la-empresa-claro.webp)

![Paso 17: Espera la revisión de la empresa (celular)](../../../salida/manual/img/tercero/aceptar-invitacion-persona-juridica/17-espera-la-revision-de-la-empresa-celular.webp)

**Variantes**

- **La empresa observa un documento:** Recibes un correo con los motivos y la frase cambia a «te pide corregir lo siguiente»: corriges lo observado y envías tu respuesta.
- **La empresa aprueba:** La frase cambia a «aprobó tu información. Tu vínculo está activo».

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](../configuracion/tipo-de-documento-y-flujo.md)
