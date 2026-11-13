---
id: "acceso/codigos-de-recuperacion"
titulo: "Genera códigos de recuperación por si pierdes tu passkey"
actor: "Administrador de empresa"
dominio: "acceso"
resumen: "Ana genera diez códigos de recuperación en Mi cuenta. Cada uno sirve una sola vez. Si pierde su passkey, entra con el código del correo, usa uno de los códigos en lugar de la passkey y registra una nueva."
pasos: 5
---

# Genera códigos de recuperación por si pierdes tu passkey

**Actor:** Administrador de empresa · **Dominio:** Acceso y cuenta

Ana genera diez códigos de recuperación en Mi cuenta. Cada uno sirve una sola vez. Si pierde su passkey, entra con el código del correo, usa uno de los códigos en lugar de la passkey y registra una nueva.

**Antes de empezar**

- Un grupo con ingreso directo y Ana como administradora de todo el grupo, con su passkey ya registrada.
- Un autenticador de prueba que simula la huella o el PIN del dispositivo.

**Pasos**

1. Abre los códigos de recuperación en Mi cuenta
2. Guarda los diez códigos ahora
3. Confirma que los guardaste
4. Usa un código si no tienes tu passkey
5. Registra una passkey nueva después del código

## Paso 1. Abre los códigos de recuperación en Mi cuenta

En Mi cuenta, la sección «Códigos de recuperación» muestra si tienes códigos activos. Hoy no tienes ninguno. Son el respaldo para entrar si pierdes el dispositivo donde guardas tu passkey.

> **Pantalla:** `/e/{empresa}/mi-cuenta`

![Paso 1: Abre los códigos de recuperación en Mi cuenta (escritorio)](../../../salida/manual/img/acceso/codigos-de-recuperacion/01-abre-los-codigos-de-recuperacion-en-mi-cuenta-claro.webp)

## Paso 2. Guarda los diez códigos ahora

Validasoft muestra diez códigos una sola vez. Antes de verlos, el navegador te pidió confirmar con tu passkey. Cada código tiene la forma XXXXX-XXXXX y sirve una sola vez. Guárdalos en un lugar seguro, aparte de tu correo: si los pierdes, no vuelven a mostrarse.

> **Pantalla:** `/e/{empresa}/mi-cuenta`

![Paso 2: Guarda los diez códigos ahora (escritorio)](../../../salida/manual/img/acceso/codigos-de-recuperacion/02-guarda-los-diez-codigos-ahora-claro.webp)

**Variantes**

- **Ya tienes códigos y generas unos nuevos:** Los anteriores dejan de servir y te muestran diez nuevos, una sola vez.

## Paso 3. Confirma que los guardaste

Marca «Ya los guardé en un lugar seguro» para cerrar la hoja. Después Mi cuenta indica cuántos códigos quedan sin usar. Los códigos no vuelven a verse aunque abras de nuevo la sección.

> **Pantalla:** `/e/{empresa}/mi-cuenta`

![Paso 3: Confirma que los guardaste (escritorio)](../../../salida/manual/img/acceso/codigos-de-recuperacion/03-confirma-que-los-guardaste-claro.webp)

## Paso 4. Usa un código si no tienes tu passkey

Si pierdes tu passkey, en el paso de confirmación pulsa «No tengo mi passkey». Escribe uno de los códigos guardados y pulsa «Usar el código». El código solo abre la pantalla para registrar una passkey nueva.

> **Pantalla:** `/`

![Paso 4: Usa un código si no tienes tu passkey (escritorio)](../../../salida/manual/img/acceso/codigos-de-recuperacion/04-usa-un-codigo-si-no-tienes-tu-passkey-claro.webp)

**Variantes**

- **El código ya se usó o está mal escrito:** La pantalla muestra un aviso y no te deja entrar.

## Paso 5. Registra una passkey nueva después del código

Con el código entras solo para registrar una passkey nueva. Después de registrarla, Mi cuenta muestra que te quedan nueve códigos sin usar. Un código usado no vuelve a servir.

> **Pantalla:** `/e/{empresa}/mi-cuenta`

![Paso 5: Registra una passkey nueva después del código (escritorio)](../../../salida/manual/img/acceso/codigos-de-recuperacion/05-registra-una-passkey-nueva-despues-del-codigo-claro.webp)

## Recorridos relacionados

- [Registra una passkey y entra con tu huella o tu rostro](ingreso-con-passkey.md)
- [Revisa tus sesiones abiertas y cierra la que no reconoces](mi-cuenta-sesiones-y-codigos.md)
- [Elige cómo entran las personas de tu grupo y exige passkey](ingreso-configuracion-del-grupo.md)
