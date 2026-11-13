---
id: "acceso/sso-configurar"
titulo: "Configura el inicio de sesión único con el proveedor de tu empresa"
actor: "Administrador de empresa"
dominio: "acceso"
resumen: "Ana conecta Validasoft con el proveedor de identidad de su empresa (OIDC): le da el nombre del botón, el emisor, el identificador del cliente y el secreto, y declara el dominio de correo de su empresa. Validasoft pide verificar el dominio con un registro DNS. El secreto se guarda y no vuelve a mostrarse."
pasos: 3
---

# Configura el inicio de sesión único con el proveedor de tu empresa

**Actor:** Administrador de empresa · **Dominio:** Acceso y cuenta

Ana conecta Validasoft con el proveedor de identidad de su empresa (OIDC): le da el nombre del botón, el emisor, el identificador del cliente y el secreto, y declara el dominio de correo de su empresa. Validasoft pide verificar el dominio con un registro DNS. El secreto se guarda y no vuelve a mostrarse.

**Antes de empezar**

- Un grupo con ingreso directo y Ana como administradora de todo el grupo, con su passkey registrada.
- Un proveedor de identidad de prueba: el emisor es una dirección ficticia y nada se conecta a un proveedor real.

**Pasos**

1. Abre la configuración del inicio de sesión único
2. Declara el dominio de correo de tu empresa
3. Guarda la configuración

## Paso 1. Abre la configuración del inicio de sesión único

En Configuración › Inicio de sesión único, la empresa conecta su proveedor de identidad. Hoy está «Sin configurar». Los datos del proveedor son el nombre del botón que verán las personas, el emisor (issuer), el Client ID y el secreto del cliente.

> **Pantalla:** `/e/{empresa}/sso`

![Paso 1: Abre la configuración del inicio de sesión único (escritorio)](../../../salida/manual/img/acceso/sso-configurar/01-abre-la-configuracion-del-inicio-de-sesion-unico-claro.webp)

**Configuración que lo afecta**

- **Inicio de sesión único (SSO)** (`Configuración › Ingreso › Inicio de sesión único (/sso)`): Con SSO configurado, las personas de la empresa pueden entrar con la cuenta de su proveedor.

## Paso 2. Declara el dominio de correo de tu empresa

Escribe el dominio de los correos de tu empresa y pulsa «Agregar». Validasoft muestra el dominio como «Por verificar» y el texto validasoft-verificacion=… que debes publicar en el archivo /.well-known/validasoft-verificacion.txt de ese dominio, y luego pulsar «Verificar». El inicio de sesión único solo vale para los correos de dominios que demuestres que son tuyos: mientras esté por verificar, no aplica a sus correos.

> **Pantalla:** `/e/{empresa}/sso`

![Paso 2: Declara el dominio de correo de tu empresa (escritorio)](../../../salida/manual/img/acceso/sso-configurar/02-declara-el-dominio-de-correo-de-tu-empresa-claro.webp)

## Paso 3. Guarda la configuración

Al guardar, Validasoft pide confirmar con tu passkey porque es una acción sensible. Después, la pantalla solo muestra con qué cuatro caracteres termina el secreto: el secreto completo nunca vuelve a verse. La pantalla indica que la configuración está «Apagado».

> **Pantalla:** `/e/{empresa}/sso`

![Paso 3: Guarda la configuración (escritorio)](../../../salida/manual/img/acceso/sso-configurar/03-guarda-la-configuracion-claro.webp)

## Recorridos relacionados

- [Elige cómo entran las personas de tu grupo y exige passkey](ingreso-configuracion-del-grupo.md)
- [Registra una passkey y entra con tu huella o tu rostro](ingreso-con-passkey.md)
