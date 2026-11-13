---
id: "tercero/subir-documento-con-formulario"
titulo: "Sube un documento que pide una fecha de expedición"
actor: "Tercero"
dominio: "tercero"
resumen: "La empresa pide el certificado de seguridad y salud en el trabajo. Como tiene fecha de expedición y vigencia, el tercero sube el archivo y escribe la fecha; Validasoft calcula cuándo vence y avisa antes de que pase."
pasos: 4
---

# Sube un documento que pide una fecha de expedición

**Actor:** Tercero · **Dominio:** Portal del tercero

La empresa pide el certificado de seguridad y salud en el trabajo. Como tiene fecha de expedición y vigencia, el tercero sube el archivo y escribe la fecha; Validasoft calcula cuándo vence y avisa antes de que pase.

**Antes de empezar**

- Un grupo empresarial con «Constructora Sur S.A.S.» y su flujo de Proveedor, que pide el RUT y el certificado de seguridad y salud en el trabajo (vence a los 30 días de su expedición, avisa 10 días antes y da 5 días de gracia).
- Un tercero persona jurídica invitado a ese flujo, que ya aceptó la invitación.

**Pasos**

1. Encuentra el certificado en «Lo que te pide»
2. Escribe la fecha de expedición y elige el archivo
3. Confirma la versión y su vigencia
4. Revisa cómo quedó el documento en tu revisión

## Paso 1. Encuentra el certificado en «Lo que te pide»

En «Lo que te pide» aparece el certificado de seguridad y salud en el trabajo, con las reglas de su tipo: formato PDF, JPG, PNG o WebP y tamaño máximo. Como este documento tiene fecha de expedición, la pantalla de carga te pedirá esa fecha además del archivo. Pulsa «Subir documento» para abrirlo.

> **Pantalla:** `/expediente/{id}`

![Paso 1: Encuentra el certificado en «Lo que te pide» (escritorio)](../../../salida/manual/img/tercero/subir-documento-con-formulario/01-encuentra-el-certificado-en-lo-que-te-pide-claro.webp)

![Paso 1: Encuentra el certificado en «Lo que te pide» (celular)](../../../salida/manual/img/tercero/subir-documento-con-formulario/01-encuentra-el-certificado-en-lo-que-te-pide-celular.webp)

**Variantes**

- **El documento no tiene campos propios:** La pantalla de carga solo pide el archivo.
- **Ya subiste una versión:** El botón dice «Subir otra versión» y la nueva versión reemplaza la vigente en revisión.

**Configuración que lo afecta**

- **Campos del formulario, vigencia y aviso del tipo de documento** (`Configuración › Tipos de documento (/tipos-documento)`): Define la fecha de expedición que se pide, el plazo de vigencia y cuántos días antes se avisa.

## Paso 2. Escribe la fecha de expedición y elige el archivo

Escribe la fecha de expedición del certificado en el selector de fecha (día, mes y año) y luego pulsa «Elegir archivo». Validasoft usa esa fecha para calcular cuándo vence: el certificado vale 30 días desde su expedición. Si el archivo no cumple el formato o el tamaño, la pantalla te lo dice y no guarda nada.

> **Pantalla:** `/boveda/subir`

![Paso 2: Escribe la fecha de expedición y elige el archivo (escritorio)](../../../salida/manual/img/tercero/subir-documento-con-formulario/02-escribe-la-fecha-de-expedicion-y-elige-el-archiv-claro.webp)

![Paso 2: Escribe la fecha de expedición y elige el archivo (celular)](../../../salida/manual/img/tercero/subir-documento-con-formulario/02-escribe-la-fecha-de-expedicion-y-elige-el-archiv-celular.webp)

## Paso 3. Confirma la versión y su vigencia

Validasoft confirma «Es tu versión 1» del certificado y dice qué empresa ya lo tiene. El documento guarda su fecha de expedición y la fecha en que vence; cuando falten 10 días te avisamos por correo. Si el documento vence, tienes 5 días de gracia para subir uno nuevo antes de que la empresa lo marque como vencido.

> **Pantalla:** `/boveda/subir`

![Paso 3: Confirma la versión y su vigencia (escritorio)](../../../salida/manual/img/tercero/subir-documento-con-formulario/03-confirma-la-version-y-su-vigencia-claro.webp)

![Paso 3: Confirma la versión y su vigencia (celular)](../../../salida/manual/img/tercero/subir-documento-con-formulario/03-confirma-la-version-y-su-vigencia-celular.webp)

**Configuración que lo afecta**

- **Vigencia, aviso y gracia del certificado** (`Configuración › Tipos de documento (/tipos-documento)`): Fija cuántos días vale el documento, cuándo se avisa y cuánta gracia tiene antes de vencerse.

## Paso 4. Revisa cómo quedó el documento en tu revisión

En «Lo que te pide» el certificado ya aparece con su versión y su vigencia («Vigente» o «Por vencer»), y muestra la fecha en que vence. Mientras tanto la empresa lo revisa; si lo observa, verás el motivo en esta misma tarjeta.

> **Pantalla:** `/expediente/{id}`

![Paso 4: Revisa cómo quedó el documento en tu revisión (escritorio)](../../../salida/manual/img/tercero/subir-documento-con-formulario/04-revisa-como-quedo-el-documento-en-tu-revision-claro.webp)

![Paso 4: Revisa cómo quedó el documento en tu revisión (celular)](../../../salida/manual/img/tercero/subir-documento-con-formulario/04-revisa-como-quedo-el-documento-en-tu-revision-celular.webp)

## Recorridos relacionados

- [Acepta la invitación y completa tu información como persona jurídica](aceptar-invitacion-persona-juridica.md)
- [Atiende un documento que está por vencer](documento-por-vencer-y-vencido.md)
