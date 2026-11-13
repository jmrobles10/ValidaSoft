---
id: "configuracion/dominio-de-correo"
titulo: "Registra el dominio de correo y verifícalo"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora registra un dominio propio para que los correos de la empresa salgan desde él, crea los registros DNS que pide Validasoft y lo verifica. Después quita el dominio y el correo vuelve al remitente de la plataforma."
pasos: 5
---

# Registra el dominio de correo y verifícalo

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora registra un dominio propio para que los correos de la empresa salgan desde él, crea los registros DNS que pide Validasoft y lo verifica. Después quita el dominio y el correo vuelve al remitente de la plataforma.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.», sin dominio de correo registrado.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Dominio de correo
2. Escribe el dominio y el nombre del remitente
3. Crea los registros DNS en tu dominio
4. Verifica el dominio
5. Quita el dominio de correo

## Paso 1. Abre Configuración › Dominio de correo

Por omisión, los correos de la empresa salen desde el remitente de la plataforma. Esta pantalla sirve para que salgan desde tu propio dominio, por ejemplo proveedores.miempresa.com. La tarjeta «Cómo sale hoy el correo» muestra desde qué remitente sale ahora. Mientras no verifiques tu dominio, nada se corta: el correo sigue saliendo desde el de la plataforma.

> **Pantalla:** `/e/{empresa}/dominio-correo`

![Paso 1: Abre Configuración › Dominio de correo (escritorio)](../../../salida/manual/img/configuracion/dominio-de-correo/01-abre-configuracion-dominio-de-correo-claro.webp)

**Variantes**

- **No tienes permiso para administrar la empresa:** La pantalla no muestra el formulario ni los registros: solo se puede ver con permiso de administración.

**Configuración que lo afecta**

- **Dominio de correo** (`Configuración › Dominio de correo (/dominio-correo)`): Define desde qué dirección de correo le escribe Validasoft a los terceros de la empresa.

## Paso 2. Escribe el dominio y el nombre del remitente

Escribe el dominio sin http:// ni la arroba. «Antes de la arroba» viene como no-responder, y «Nombre del remitente» como el nombre de la empresa; ambos puedes cambiarlos. La línea «Cuando lo verifiques, el correo saldrá de» muestra la dirección exacta mientras escribes, para que la revises antes de guardar.

> **Pantalla:** `/e/{empresa}/dominio-correo`

![Paso 2: Escribe el dominio y el nombre del remitente (escritorio)](../../../salida/manual/img/configuracion/dominio-de-correo/02-escribe-el-dominio-y-el-nombre-del-remitente-claro.webp)

**Variantes**

- **El dominio tiene un formato inválido:** El campo se marca en rojo con la explicación del error y el botón «Registrar el dominio» no guarda.
- **Ya hay un dominio y cambias el nombre del dominio:** El botón dice «Guardar los cambios» y aparece el aviso de que cambiar el dominio pide verificarlo de nuevo.

## Paso 3. Crea los registros DNS en tu dominio

Al registrar el dominio aparecen tres registros que debes crear en el panel donde administras el dominio: SPF, DKIM y DMARC. Cada uno tiene su tipo, su nombre y su valor, y un botón para copiarlo. SPF autoriza a la plataforma a enviar en nombre de tu dominio, DKIM firma cada correo y DMARC indica qué hacer con lo que no pase esas dos comprobaciones. Los cambios de DNS pueden tardar en propagarse.

> **Pantalla:** `/e/{empresa}/dominio-correo`

![Paso 3: Crea los registros DNS en tu dominio (escritorio)](../../../salida/manual/img/configuracion/dominio-de-correo/03-crea-los-registros-dns-en-tu-dominio-claro.webp)

**Variantes**

- **Cambias o quitas el dominio después:** Los registros que ya creaste en tu DNS no se borran automáticamente: quítalos tú en el panel de tu dominio.

## Paso 4. Verifica el dominio

«Verificar ahora» consulta los tres registros en el DNS. Si todos están bien, el estado pasa a «Verificado» y el correo de la empresa sale desde su propio dominio. Si falta alguno, el estado dice «Falta algún registro», cada registro muestra si no se encontró o si tiene otro valor, y el correo sigue saliendo desde el remitente de la plataforma hasta que todo quede bien.

> **Pantalla:** `/e/{empresa}/dominio-correo`

![Paso 4: Verifica el dominio (escritorio)](../../../salida/manual/img/configuracion/dominio-de-correo/04-verifica-el-dominio-claro.webp)

**Variantes**

- **Falta un registro:** El registro aparece con «No encontrado» o «Distinto» y la verificación dice cuántos faltan.
- **El dominio ya está verificado:** El botón «Verificar ahora» sigue disponible por si cambia el DNS.

**Configuración que lo afecta**

- **Registros DNS del dominio** (`Configuración › Dominio de correo (/dominio-correo)`): Solo cuando los tres registros están bien el correo sale desde el dominio propio.

## Paso 5. Quita el dominio de correo

«Quitar el dominio» pide confirmación y explica que el correo vuelve a salir desde el remitente de la plataforma. Al confirmar, el formulario queda vacío y la tarjeta «Cómo sale hoy el correo» vuelve a mostrar ese remitente. Los registros DNS que creaste no se borran: quítalos tú en el panel de tu dominio si ya no los necesitas.

> **Pantalla:** `/e/{empresa}/dominio-correo`

![Paso 5: Quita el dominio de correo (escritorio)](../../../salida/manual/img/configuracion/dominio-de-correo/05-quita-el-dominio-de-correo-claro.webp)

## Recorridos relacionados

- [Personaliza el correo de invitación y restablécelo](plantillas-de-correo.md)
