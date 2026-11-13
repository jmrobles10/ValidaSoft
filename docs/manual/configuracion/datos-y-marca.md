---
id: "configuracion/datos-y-marca"
titulo: "Cambia el contacto, el logo y el color de la marca"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora actualiza el contacto de soporte, el logo y el color de marca de la empresa. Esos datos son lo que el tercero ve en su portal y en los correos, y lo que aparece en los PDF."
pasos: 4
---

# Cambia el contacto, el logo y el color de la marca

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora actualiza el contacto de soporte, el logo y el color de marca de la empresa. Esos datos son lo que el tercero ve en su portal y en los correos, y lo que aparece en los PDF.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.», sin logo y con el color de marca por omisión.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Datos y marca
2. Escribe el contacto de soporte
3. Sube el logo y elige el color de marca
4. Guarda los cambios

## Paso 1. Abre Configuración › Datos y marca

La pantalla tiene dos partes. «Datos de la empresa» muestra la razón social, la identificación y el prefijo de folio: se ven, pero no se cambian aquí, porque el prefijo ya aparece en los folios emitidos. La tarjeta «Marca en el portal del tercero» es lo que sí puedes editar: el contacto de soporte, el logo y el color.

> **Pantalla:** `/e/{empresa}/datos-marca`

![Paso 1: Abre Configuración › Datos y marca (escritorio)](../../../salida/manual/img/configuracion/datos-y-marca/01-abre-configuracion-datos-y-marca-claro.webp)

**Variantes**

- **No tienes permiso para administrar la empresa:** La pantalla no muestra la página: solo la administración de la empresa puede cambiar los datos y la marca.

**Configuración que lo afecta**

- **Datos y marca de la empresa** (`Configuración › Datos y marca (/datos-marca)`): Lo que el tercero ve en su portal y en los correos de la empresa.

## Paso 2. Escribe el contacto de soporte

El contacto de soporte es un correo. Aparece al pie del portal del tercero y es la dirección de respuesta de los correos que envía Validasoft en nombre de la empresa: si el tercero responde a un correo, le llega a esta dirección. Si escribes un correo mal formado, el campo lo marca y el botón «Guardar cambios» no se activa.

> **Pantalla:** `/e/{empresa}/datos-marca`

![Paso 2: Escribe el contacto de soporte (escritorio)](../../../salida/manual/img/configuracion/datos-y-marca/02-escribe-el-contacto-de-soporte-claro.webp)

**Variantes**

- **El correo no es válido:** El campo dice «Escribe un correo válido» y no se puede guardar.

**Configuración que lo afecta**

- **Contacto de soporte para terceros** (`Configuración › Datos y marca (/datos-marca)`): Es la dirección de respuesta de los correos y aparece en el pie del portal.

## Paso 3. Sube el logo y elige el color de marca

Con «Cambiar logo» subes un archivo SVG o PNG con fondo transparente, de máximo 200 KB. El color de marca se escribe en hexadecimal, por ejemplo #1F6F5C. La caja «Así lo verá el tercero» muestra un botón y un enlace con los colores que Validasoft deriva de tu color, ajustados para que el texto se lea bien. El logo y el color se usan en el portal, en los correos y en los PDF que genera Validasoft, como el certificado y la declaración PEP.

> **Pantalla:** `/e/{empresa}/datos-marca`

![Paso 3: Sube el logo y elige el color de marca (escritorio)](../../../salida/manual/img/configuracion/datos-y-marca/03-sube-el-logo-y-elige-el-color-de-marca-claro.webp)

**Variantes**

- **El archivo pesa más de 200 KB o no es SVG ni PNG:** Aparece el mensaje de error debajo del botón y el logo no cambia.
- **El color no tiene el formato #RRGGBB:** El campo dice «Usa el formato #RRGGBB» y la vista previa no cambia.
- **Quitas el logo:** Se usa la marca por omisión de Validasoft en el portal y los correos.

## Paso 4. Guarda los cambios

«Guardar cambios» se activa solo cuando hay algo distinto de lo guardado. Al guardar aparece el aviso «Datos y marca guardados» y el botón vuelve a quedar desactivado. A partir de ahora, el portal del tercero y los correos nuevos usan el contacto, el logo y el color guardados.

> **Pantalla:** `/e/{empresa}/datos-marca`

![Paso 4: Guarda los cambios (escritorio)](../../../salida/manual/img/configuracion/datos-y-marca/04-guarda-los-cambios-claro.webp)

## Recorridos relacionados

- [Personaliza el correo de invitación y restablécelo](plantillas-de-correo.md)
- [Registra el dominio de correo y verifícalo](dominio-de-correo.md)
