---
id: "acceso/ingreso-configuracion-del-grupo"
titulo: "Elige cómo entran las personas de tu grupo y exige passkey"
actor: "Administrador de empresa"
dominio: "acceso"
resumen: "Ana, como administradora del grupo, define en Configuración › Ingreso cómo entran las personas: desde el sistema integrado, directo a Validasoft con correo y código, o ambos. También elige si todas las personas deben registrar una passkey."
pasos: 3
---

# Elige cómo entran las personas de tu grupo y exige passkey

**Actor:** Administrador de empresa · **Dominio:** Acceso y cuenta

Ana, como administradora del grupo, define en Configuración › Ingreso cómo entran las personas: desde el sistema integrado, directo a Validasoft con correo y código, o ambos. También elige si todas las personas deben registrar una passkey.

**Antes de empezar**

- Un grupo con ingreso directo activo y Ana como administradora de todo el grupo, con su passkey registrada.

**Pasos**

1. Abre la política de ingreso del grupo
2. Exige passkey a todas las personas
3. Guarda la política de ingreso

## Paso 1. Abre la política de ingreso del grupo

En Configuración › Ingreso eliges cómo entran las personas del grupo. «Desde el sistema integrado» es para quienes entran por tu sistema; «Directo a Validasoft» es para quienes entran con su correo y un código. Puedes admitir uno o ambos. Quien entra desde el sistema integrado no cambia aunque apagues el modo directo.

> **Pantalla:** `/e/{empresa}/ingreso`

![Paso 1: Abre la política de ingreso del grupo (escritorio)](../../../salida/manual/img/acceso/ingreso-configuracion-del-grupo/01-abre-la-politica-de-ingreso-del-grupo-claro.webp)

**Variantes**

- **Apagas el ingreso directo:** Las sesiones directas abiertas se cierran y la pantalla avisa de ese efecto antes de guardar.
- **Desactivas el modo «Desde el sistema integrado»:** Las pantallas embebidas de tus sistemas dejan de abrir sesión.

**Configuración que lo afecta**

- **Cómo entran (modos de ingreso)** (`Configuración › Ingreso (/ingreso)`): Define si las personas pueden entrar directo a Validasoft, desde el sistema integrado, o desde los dos.

## Paso 2. Exige passkey a todas las personas

Activa «Exigir passkey a todas las personas del grupo» para que cada persona registre una passkey en su próximo ingreso. Los administradores, oficiales de cumplimiento y auditores ya la necesitan siempre; esta opción extiende la regla a revisores y demás personas.

> **Pantalla:** `/e/{empresa}/ingreso`

![Paso 2: Exige passkey a todas las personas (escritorio)](../../../salida/manual/img/acceso/ingreso-configuracion-del-grupo/02-exige-passkey-a-todas-las-personas-claro.webp)

**Configuración que lo afecta**

- **Exigir passkey a todas las personas del grupo** (`Configuración › Ingreso (/ingreso)`): Cada persona debe registrar una passkey la próxima vez que ingrese directo.

## Paso 3. Guarda la política de ingreso

Al pulsar «Guardar», la política queda aplicada al grupo. Si cambiaste algo sin guardar, la pantalla avisa «Tienes cambios sin guardar» hasta que lo hagas.

> **Pantalla:** `/e/{empresa}/ingreso`

![Paso 3: Guarda la política de ingreso (escritorio)](../../../salida/manual/img/acceso/ingreso-configuracion-del-grupo/03-guarda-la-politica-de-ingreso-claro.webp)

## Recorridos relacionados

- [Entra a Validasoft con tu correo y un código](ingreso-directo-por-correo.md)
- [Registra una passkey y entra con tu huella o tu rostro](ingreso-con-passkey.md)
- [Configura el inicio de sesión único con el proveedor de tu empresa](sso-configurar.md)
- [Revisa el plan del grupo y su uso del mes](../configuracion/plan-del-grupo.md)
