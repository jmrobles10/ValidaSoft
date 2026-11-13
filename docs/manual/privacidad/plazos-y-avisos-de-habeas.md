---
id: "privacidad/plazos-y-avisos-de-habeas"
titulo: "Atiende los avisos de plazo de una solicitud de habeas data"
actor: "Administrador de empresa"
dominio: "privacidad"
resumen: "Una solicitud de habeas data se acerca a su fecha límite: la administradora recibe un aviso en su campana y por correo cuando le quedan dos días hábiles o menos, y otro cuando el plazo legal ya pasó. Cada aviso lleva a la solicitud para responderla."
pasos: 5
---

# Atiende los avisos de plazo de una solicitud de habeas data

**Actor:** Administrador de empresa · **Dominio:** Datos personales

Una solicitud de habeas data se acerca a su fecha límite: la administradora recibe un aviso en su campana y por correo cuando le quedan dos días hábiles o menos, y otro cuando el plazo legal ya pasó. Cada aviso lleva a la solicitud para responderla.

**Antes de empezar**

- Un grupo con Constructora Norte, un proveedor con su expediente completado y una solicitud de habeas data de conocer sus datos dirigida a la empresa.
- Plataforma corre el barrido de plazos con una fecha simulada: un día antes del vencimiento y un día después (en producción corre solo).

**Pasos**

1. Mira la fecha límite de la solicitud
2. Recibe el aviso de plazo por vencer
3. Abre la solicitud desde el aviso
4. Recibe el aviso de plazo vencido
5. Recibe el mismo aviso por correo

## Paso 1. Mira la fecha límite de la solicitud

Cada solicitud dice hasta cuándo debe responderse (1) y cuántos días hábiles quedan: la ley da 10 o 15 días hábiles según el derecho que ejerce el titular. Nadie tiene que acordarse: Validasoft revisa los plazos de las solicitudes abiertas y avisa a quienes deben responder cuando la fecha se acerca y cuando pasa.

> **Pantalla:** `/e/{empresa}/habeas-data`

![Paso 1: Mira la fecha límite de la solicitud (escritorio)](../../../salida/manual/img/privacidad/plazos-y-avisos-de-habeas/01-mira-la-fecha-limite-de-la-solicitud-claro.webp)

**Variantes**

- **La solicitud se dirige a Validasoft y no a una empresa:** El aviso va a los correos de la plataforma que se configuran en el servidor, no a la campana de ninguna empresa.

**Configuración que lo afecta**

- **Quién responde el habeas data** (`Configuración › Usuarios y roles (/usuarios)`): Reciben los avisos los administradores (del grupo o de la empresa) y el oficial de cumplimiento de la empresa.

## Paso 2. Recibe el aviso de plazo por vencer

Cuando a una solicitud le quedan dos días hábiles o menos, la campana de la administradora y del oficial de cumplimiento muestra «Vence pronto una solicitud de…» (1), con el radicado, la fecha límite y los días que quedan. El aviso llega una sola vez por solicitud y también por correo, salvo que la persona haya apagado ese tipo de aviso en sus preferencias.

> **Pantalla:** `/e/{empresa}/habeas-data`

![Paso 2: Recibe el aviso de plazo por vencer (escritorio)](../../../salida/manual/img/privacidad/plazos-y-avisos-de-habeas/02-recibe-el-aviso-de-plazo-por-vencer-claro.webp)

**Configuración que lo afecta**

- **Preferencias de notificaciones** (`Campana › Preferencias`): Cada persona decide si le llega por correo el aviso de plazo de habeas data; la campana siempre lo muestra.

## Paso 3. Abre la solicitud desde el aviso

Al abrir el aviso, la campana lleva directo a «Habeas data», donde está la solicitud (1) con su radicado, el titular y el plazo. Desde ahí se responde con «Responder»; al enviarla, la solicitud sale de las abiertas y no vuelve a generar avisos.

> **Pantalla:** `/e/{empresa}/habeas-data`

![Paso 3: Abre la solicitud desde el aviso (escritorio)](../../../salida/manual/img/privacidad/plazos-y-avisos-de-habeas/03-abre-la-solicitud-desde-el-aviso-claro.webp)

## Paso 4. Recibe el aviso de plazo vencido

Si la fecha límite pasa sin respuesta, llega un segundo aviso: «Venció el plazo de una solicitud de…» (1), que dice que ya pasó el plazo legal y pide responder cuanto antes. Es otro aviso distinto del anterior, y también sale una sola vez. La solicitud sigue abierta hasta que se responda.

> **Pantalla:** `/e/{empresa}/habeas-data`

![Paso 4: Recibe el aviso de plazo vencido (escritorio)](../../../salida/manual/img/privacidad/plazos-y-avisos-de-habeas/04-recibe-el-aviso-de-plazo-vencido-claro.webp)

## Paso 5. Recibe el mismo aviso por correo

El correo repite el aviso con el radicado, la fecha en que venció y el plazo legal, e invita a responder. Llega a cada administrador y al oficial de cumplimiento de la empresa, uno por persona.

> **Pantalla:** `/`

![Paso 5: Recibe el mismo aviso por correo (escritorio)](../../../salida/manual/img/privacidad/plazos-y-avisos-de-habeas/05-recibe-el-mismo-aviso-por-correo-claro.webp)

## Recorridos relacionados

- [Responde una solicitud de habeas data del titular](habeas-data-de-la-empresa.md)
- [Pide conocer, corregir o suprimir tus datos personales](solicitud-sobre-mis-datos.md)
