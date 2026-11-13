---
id: "configuracion/plantillas-de-correo"
titulo: "Personaliza el correo de invitación y restablécelo"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora abre Plantillas de correo, cambia el asunto, la cabecera y el pie del correo de invitación y lo guarda. Después restablece el texto predeterminado de Validasoft."
pasos: 4
---

# Personaliza el correo de invitación y restablécelo

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora abre Plantillas de correo, cambia el asunto, la cabecera y el pie del correo de invitación y lo guarda. Después restablece el texto predeterminado de Validasoft.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y sus plantillas de correo sin personalizar.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Plantillas de correo
2. Elige el correo que quieres cambiar
3. Revisa la plantilla personalizada
4. Restablece el texto predeterminado

## Paso 1. Abre Configuración › Plantillas de correo

La tabla lista los correos que Validasoft le envía al tercero en nombre de la empresa, por ejemplo la invitación. Cada fila muestra si el correo es «Predeterminada» (sale con el texto de Validasoft) o «Personalizada» (tiene un asunto, una cabecera o un pie propios) y su versión. Validasoft arma el cuerpo del correo (enlaces, documentos, plazos); tú solo agregas un texto antes y otro después.

> **Pantalla:** `/e/{empresa}/plantillas`

![Paso 1: Abre Configuración › Plantillas de correo (escritorio)](../../../salida/manual/img/configuracion/plantillas-de-correo/01-abre-configuracion-plantillas-de-correo-claro.webp)

**Variantes**

- **No tienes permiso de configuración:** El botón de cada fila dice «Ver» y el panel se abre en solo lectura, con un aviso que lo indica.

**Configuración que lo afecta**

- **Plantillas de correo** (`Configuración › Plantillas de correo (/plantillas)`): El asunto, la cabecera y el pie que se usan en cada correo del grupo para la empresa activa.

## Paso 2. Elige el correo que quieres cambiar

Haz clic en «Editar» en la fila del correo (aquí, la invitación). Se abre un panel lateral con el asunto, la cabecera y el pie del correo. Los campos vacíos usan el texto de Validasoft, así que puedes cambiar solo el que necesites.

> **Pantalla:** `/e/{empresa}/plantillas`

![Paso 2: Elige el correo que quieres cambiar (escritorio)](../../../salida/manual/img/configuracion/plantillas-de-correo/02-elige-el-correo-que-quieres-cambiar-claro.webp)

**Variantes**

- **El correo ya es «Personalizada»:** Los campos aparecen con el texto que guardaste y el panel ofrece «Restaurar predeterminado».

## Paso 3. Revisa la plantilla personalizada

Al guardar, el panel se cierra y la fila pasa a «Personalizada». Mientras escribías, la vista previa del correo se actualizaba en el mismo panel con datos de ejemplo. Para insertar un dato real en el texto, usa «Insertar variable»: por ejemplo {{empresa}} se reemplaza por el nombre de la empresa en cada correo. Los próximos correos de invitación saldrán con el asunto, la cabecera y el pie nuevos; los que ya se enviaron no cambian.

> **Pantalla:** `/e/{empresa}/plantillas`

![Paso 3: Revisa la plantilla personalizada (escritorio)](../../../salida/manual/img/configuracion/plantillas-de-correo/03-revisa-la-plantilla-personalizada-claro.webp)

**Variantes**

- **Dejas un campo vacío y guardas:** Ese campo vuelve al texto de Validasoft y el correo sigue «Personalizada» si otro campo tiene texto.

**Configuración que lo afecta**

- **Asunto, cabecera y pie del correo** (`Configuración › Plantillas de correo (/plantillas)`): Aplica a los correos que se envían después de guardar.

## Paso 4. Restablece el texto predeterminado

«Restaurar predeterminado» abre una confirmación que quita el asunto, la cabecera y el pie de la plantilla; el texto vuelve al de Validasoft. Al confirmar, la fila vuelve a «Predeterminada» y el aviso confirma el cambio. Si cierras el panel con «Cancelar» o la «X», no se guarda ningún cambio pendiente.

> **Pantalla:** `/e/{empresa}/plantillas`

![Paso 4: Restablece el texto predeterminado (escritorio)](../../../salida/manual/img/configuracion/plantillas-de-correo/04-restablece-el-texto-predeterminado-claro.webp)

**Variantes**

- **El correo no está personalizado:** No aparece «Restaurar predeterminado»: no hay nada que restablecer.

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](tipo-de-documento-y-flujo.md)
