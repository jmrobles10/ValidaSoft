---
id: "configuracion/empresas-y-textos-legales"
titulo: "Agrega una empresa del grupo y publica sus textos legales"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora agrega una razón social nueva al grupo y publica la autorización de datos y los términos que los terceros deben aceptar antes de vincularse. Sin ambos textos, la empresa no puede recibir aceptaciones."
pasos: 6
---

# Agrega una empresa del grupo y publica sus textos legales

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora agrega una razón social nueva al grupo y publica la autorización de datos y los términos que los terceros deben aceptar antes de vincularse. Sin ambos textos, la empresa no puede recibir aceptaciones.

**Antes de empezar**

- Un grupo empresarial con las empresas «Constructora Norte S.A.S.» y «Constructora Sur S.A.S.».
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Empresas del grupo
2. Escribe los datos de la empresa
3. Revisa los textos legales de la empresa nueva
4. Escribe el texto de la autorización
5. Publica los términos y condiciones
6. Comprueba que los dos textos están vigentes

## Paso 1. Abre Configuración › Empresas del grupo

La tabla lista las razones sociales del grupo con su identificación, su prefijo de folio y si están activas. Cada empresa decide por su cuenta: no ve las decisiones de las otras, aunque compartan usuarios y configuración del grupo. Al elegir una fila se abre debajo el detalle de esa empresa, con sus textos legales.

> **Pantalla:** `/e/{empresa}/empresas`

![Paso 1: Abre Configuración › Empresas del grupo (escritorio)](../../../salida/manual/img/configuracion/empresas-y-textos-legales/01-abre-configuracion-empresas-del-grupo-claro.webp)

**Variantes**

- **Una empresa está inactiva:** Su fila sale con el estado «Inactiva» y sus textos legales no se pueden publicar de nuevo.

**Configuración que lo afecta**

- **Empresas del grupo** (`Configuración › Empresas del grupo (/empresas)`): Cada empresa tiene sus propios flujos, áreas, textos legales y folios.

## Paso 2. Escribe los datos de la empresa

Elige el tipo y el número de identificación (con o sin dígito de verificación: Validasoft lo calcula y lo compara), la razón social y el prefijo de folio. El prefijo son de 2 a 10 letras mayúsculas o números, y aparece en todos los folios de la empresa: no se puede cambiar después. El campo de ejemplo muestra cómo se verán sus folios.

> **Pantalla:** `/e/{empresa}/empresas`

![Paso 2: Escribe los datos de la empresa (escritorio)](../../../salida/manual/img/configuracion/empresas-y-textos-legales/02-escribe-los-datos-de-la-empresa-claro.webp)

**Variantes**

- **El prefijo tiene menos de 2 o más de 10 caracteres, o minúsculas:** El campo dice que debe tener entre 2 y 10 letras mayúsculas o números y no se puede agregar.
- **El número no tiene el formato del tipo elegido:** El campo dice que el número no tiene el formato de ese tipo de identificación.

## Paso 3. Revisa los textos legales de la empresa nueva

La empresa nueva aparece con sus textos legales «Sin publicar»: la autorización de tratamiento de datos y los términos y condiciones. Mientras falte alguno, los terceros no pueden aceptar las invitaciones de la empresa. Lo que el tercero acepta al vincularse es exactamente lo que publiques aquí.

> **Pantalla:** `/e/{empresa}/empresas`

![Paso 3: Revisa los textos legales de la empresa nueva (escritorio)](../../../salida/manual/img/configuracion/empresas-y-textos-legales/03-revisa-los-textos-legales-de-la-empresa-nueva-claro.webp)

**Configuración que lo afecta**

- **Textos legales de la empresa** (`Configuración › Empresas del grupo (/empresas)`): Es lo que el tercero lee y acepta antes de vincularse con la empresa.

## Paso 4. Escribe el texto de la autorización

Escribe el texto completo de la autorización de tratamiento de datos. Se publica como versión 1: el botón «Publicar versión 1» solo se activa cuando hay texto y cambió algo respecto de la versión vigente. Al pulsarlo, Validasoft pide confirmación, porque el texto queda vigente de inmediato y los terceros lo aceptan al vincularse.

> **Pantalla:** `/e/{empresa}/empresas`

![Paso 4: Escribe el texto de la autorización (escritorio)](../../../salida/manual/img/configuracion/empresas-y-textos-legales/04-escribe-el-texto-de-la-autorizacion-claro.webp)

**Variantes**

- **El texto está vacío:** El botón de publicar está desactivado y dice que falta escribir el texto.
- **Ya hay una versión vigente:** El editor muestra el aviso de que la versión nueva queda vigente y los terceros deben aceptarla de nuevo.

## Paso 5. Publica los términos y condiciones

Los términos y condiciones se publican igual que la autorización, cada uno con su propia versión. Cuando la empresa tiene los dos textos vigentes, los terceros pueden aceptar sus invitaciones; para corregir un texto se publica otra versión y el historial de versiones queda guardado.

> **Pantalla:** `/e/{empresa}/empresas`

![Paso 5: Publica los términos y condiciones (escritorio)](../../../salida/manual/img/configuracion/empresas-y-textos-legales/05-publica-los-terminos-y-condiciones-claro.webp)

## Paso 6. Comprueba que los dos textos están vigentes

Cada texto muestra su versión vigente y la fecha desde la que rige. «Ver texto» despliega lo que el tercero leerá; si un texto cambia, «Publicar versión nueva» crea la versión siguiente sin borrar las anteriores.

> **Pantalla:** `/e/{empresa}/empresas`

![Paso 6: Comprueba que los dos textos están vigentes (escritorio)](../../../salida/manual/img/configuracion/empresas-y-textos-legales/06-comprueba-que-los-dos-textos-estan-vigentes-claro.webp)

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](tipo-de-documento-y-flujo.md)
