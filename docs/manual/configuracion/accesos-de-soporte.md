---
id: "configuracion/accesos-de-soporte"
titulo: "Concede un acceso de soporte por unas horas y luego lo revoca"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "El soporte de Validasoft no ve nada del grupo salvo que se lo concedan. La administradora concede acceso a Constructora Sur por cuatro horas, con una razón, revisa cuánto le queda y, cuando ya no hace falta, lo revoca."
pasos: 5
---

# Concede un acceso de soporte por unas horas y luego lo revoca

**Actor:** Administrador de empresa · **Dominio:** Configuración

El soporte de Validasoft no ve nada del grupo salvo que se lo concedan. La administradora concede acceso a Constructora Sur por cuatro horas, con una razón, revisa cuánto le queda y, cuando ya no hace falta, lo revoca.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y sin accesos de soporte vigentes.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Accesos de soporte
2. Concede el acceso con una razón
3. Revisa el acceso concedido
4. Revoca el acceso antes de que expire
5. Comprueba que el acceso quedó revocado

## Paso 1. Abre Configuración › Accesos de soporte

El soporte de Validasoft no entra a tu grupo por su cuenta. Esta pantalla lista los accesos que has concedido: la empresa, para qué, cuándo expira y su estado. Si no hay ninguno, la tabla lo dice; cuando el soporte necesite revisar algo, concedes un acceso temporal desde «Conceder acceso».

> **Pantalla:** `/e/{empresa}/accesos-soporte`

![Paso 1: Abre Configuración › Accesos de soporte (escritorio)](../../../salida/manual/img/configuracion/accesos-de-soporte/01-abre-configuracion-accesos-de-soporte-claro.webp)

**Configuración que lo afecta**

- **Accesos de soporte** (`Configuración › Accesos de soporte (/accesos-soporte)`): El soporte solo ve lo de la empresa y el tiempo que concedas.

## Paso 2. Concede el acceso con una razón

Eliges la empresa, la duración (de 1 a 72 horas) y escribes para qué lo necesitan. La razón queda en la auditoría, así que escríbela como la leerá alguien después. El soporte podrá leer la configuración, los flujos, los vínculos y los expedientes de esa empresa. Nunca verá ni descargará documentos, ni podrá cambiar nada.

> **Pantalla:** `/e/{empresa}/accesos-soporte`

![Paso 2: Concede el acceso con una razón (escritorio)](../../../salida/manual/img/configuracion/accesos-de-soporte/02-concede-el-acceso-con-una-razon-claro.webp)

**Variantes**

- **La razón supera los 500 caracteres:** La pantalla dice «Máximo 500 caracteres» y no deja conceder el acceso.

## Paso 3. Revisa el acceso concedido

La fila muestra la empresa, la razón, la hora de expiración y el tiempo que queda, por ejemplo «Vigente · quedan 4 h 0 min». Al vencer, pasa a «Expirado» sin que tengas que hacer nada.

> **Pantalla:** `/e/{empresa}/accesos-soporte`

![Paso 3: Revisa el acceso concedido (escritorio)](../../../salida/manual/img/configuracion/accesos-de-soporte/03-revisa-el-acceso-concedido-claro.webp)

## Paso 4. Revoca el acceso antes de que expire

Si el soporte ya no lo necesita, «Revocar» corta el acceso de inmediato: deja de entrar a la empresa aunque le falten horas. Si lo vuelve a necesitar, concedes un acceso nuevo.

> **Pantalla:** `/e/{empresa}/accesos-soporte`

![Paso 4: Revoca el acceso antes de que expire (escritorio)](../../../salida/manual/img/configuracion/accesos-de-soporte/04-revoca-el-acceso-antes-de-que-expire-claro.webp)

**Variantes**

- **El acceso ya expiró:** La fila muestra «Expirado» y no ofrece revocar.

## Paso 5. Comprueba que el acceso quedó revocado

La fila queda con estado «Revocado». Queda registrado en la auditoría quién lo concedió, para qué y cuándo se revocó.

> **Pantalla:** `/e/{empresa}/accesos-soporte`

![Paso 5: Comprueba que el acceso quedó revocado (escritorio)](../../../salida/manual/img/configuracion/accesos-de-soporte/05-comprueba-que-el-acceso-quedo-revocado-claro.webp)

## Recorridos relacionados

- [Comprueba qué secciones ve cada rol](permisos-por-rol.md)
