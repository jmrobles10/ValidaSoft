---
id: "plataforma/sistemas-de-un-grupo"
titulo: "Desactiva y reactiva un sistema integrado de un grupo"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma abre los sistemas integrados de un grupo y desactiva uno que ya no debe entrar a Validasoft. Desactivarlo corta de inmediato sus llaves y sus sesiones; reactivarlo no revive lo que ya se revocó."
pasos: 5
---

# Desactiva y reactiva un sistema integrado de un grupo

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma abre los sistemas integrados de un grupo y desactiva uno que ya no debe entrar a Validasoft. Desactivarlo corta de inmediato sus llaves y sus sesiones; reactivarlo no revive lo que ya se revocó.

**Antes de empezar**

- Un grupo con un sistema integrado activo y su API key.
- Un operador de Plataforma con la consola abierta.

**Pasos**

1. Abre los sistemas integrados del grupo
2. Revisa los sistemas del grupo
3. Confirma la desactivación
4. Verifica que el sistema quedó desactivado
5. Reactiva el sistema

## Paso 1. Abre los sistemas integrados del grupo

Cada grupo tiene el botón «Sistemas» en su tarjeta. Desde allí ves los sistemas que el grupo integró con Validasoft: el sistema es el ERP o portal del cliente que autentica a sus personas y firma sus aserciones. El botón abre la lista para ese grupo; no afecta a los otros grupos.

> **Pantalla:** `/plataforma/grupos`

![Paso 1: Abre los sistemas integrados del grupo (escritorio)](../../../salida/manual/img/plataforma/sistemas-de-un-grupo/01-abre-los-sistemas-integrados-del-grupo-claro.webp)

**Configuración que lo afecta**

- **Sistemas integrados del grupo** (`Plataforma › Grupos y empresas › Sistemas`): Permite desactivar o reactivar cada sistema del grupo.

## Paso 2. Revisa los sistemas del grupo

La lista muestra cada sistema con su nombre, su fecha de creación y cuántos sitios pueden incrustar Validasoft. Desactivar un sistema cierra de inmediato el acceso de sus llaves y de las sesiones que emitió. Su historial se conserva, así que siempre se puede reactivar después.

> **Pantalla:** `/plataforma/grupos`

![Paso 2: Revisa los sistemas del grupo (escritorio)](../../../salida/manual/img/plataforma/sistemas-de-un-grupo/02-revisa-los-sistemas-del-grupo-claro.webp)

**Variantes**

- **El grupo no tiene sistemas:** La lista dice «Este grupo no tiene sistemas integrados».
- **El sistema está desactivado:** El botón cambia a «Reactivar» y el sistema deja de pedir sesiones.

**Configuración que lo afecta**

- **Estado del sistema integrado**: Un sistema desactivado no puede pedir sesiones y sus llaves dejan de servir ahora mismo.

## Paso 3. Confirma la desactivación

Antes de desactivar, Validasoft pide confirmación: el sistema deja de poder pedir sesiones y sus llaves dejan de servir en ese momento. Su historial no se borra. Si la desactivación es un error, puedes reactivarlo en la misma lista, aunque las sesiones anteriores no se recuperan.

> **Pantalla:** `/plataforma/grupos`

![Paso 3: Confirma la desactivación (escritorio)](../../../salida/manual/img/plataforma/sistemas-de-un-grupo/03-confirma-la-desactivacion-claro.webp)

**Configuración que lo afecta**

- **Desactivar un sistema integrado**: Corta las llaves y las sesiones del sistema de inmediato.

## Paso 4. Verifica que el sistema quedó desactivado

Validasoft confirma con un aviso que el sistema quedó desactivado. En la lista, su botón ahora dice «Reactivar» y el sistema ya no puede abrir sesiones para las personas del grupo. Las llaves revocadas siguen revocadas, aunque el sistema se reactive.

> **Pantalla:** `/plataforma/grupos`

![Paso 4: Verifica que el sistema quedó desactivado (escritorio)](../../../salida/manual/img/plataforma/sistemas-de-un-grupo/04-verifica-que-el-sistema-quedo-desactivado-claro.webp)

## Paso 5. Reactiva el sistema

Al reactivarlo, el sistema vuelve a poder pedir sesiones con sus llaves vigentes. Las sesiones y los tokens de antes no se recuperan, y las llaves que se revocaron no vuelven: esas se emiten de nuevo desde el grupo.

> **Pantalla:** `/plataforma/grupos`

![Paso 5: Reactiva el sistema (escritorio)](../../../salida/manual/img/plataforma/sistemas-de-un-grupo/05-reactiva-el-sistema-claro.webp)

## Recorridos relacionados

- [Crea un grupo con su primera empresa](crear-grupo-y-empresa.md)
- [Crea una API key con sus permisos y empresas, rótala y revócala](../integracion/api-keys.md)
- [Registra un sistema integrado con su clave pública](../integracion/sistema-integrador.md)
