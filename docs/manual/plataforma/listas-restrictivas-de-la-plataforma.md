---
id: "plataforma/listas-restrictivas-de-la-plataforma"
titulo: "Configura la cuenta de Infolaft de la plataforma"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma configura la única cuenta de Infolaft que usan todas las empresas para consultar listas restrictivas. Fija el usuario, la clave, el plan contratado y la cuota mensual por empresa. Las empresas la usan sin ver ni cargar credenciales propias."
pasos: 4
---

# Configura la cuenta de Infolaft de la plataforma

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma configura la única cuenta de Infolaft que usan todas las empresas para consultar listas restrictivas. Fija el usuario, la clave, el plan contratado y la cuota mensual por empresa. Las empresas la usan sin ver ni cargar credenciales propias.

**Antes de empezar**

- Un operador de Plataforma con la consola abierta.
- Una empresa del grupo que consulte listas restrictivas.

**Pasos**

1. Abre la cuenta de la plataforma
2. Escribe usuario, clave, plan y cuota
3. Guarda la cuenta y comprueba que quedó configurada
4. Así la ve una empresa del grupo

## Paso 1. Abre la cuenta de la plataforma

Infolaft es uno solo para todos los clientes: esta es la cuenta de la plataforma. Las empresas consultan con ella sin verla ni cargar la suya. Mientras no esté configurada, las empresas ven Infolaft como «No disponible».

> **Pantalla:** `/plataforma/listas`

![Paso 1: Abre la cuenta de la plataforma (escritorio)](../../../salida/manual/img/plataforma/listas-restrictivas-de-la-plataforma/01-abre-la-cuenta-de-la-plataforma-claro.webp)

**Configuración que lo afecta**

- **Cuenta de Infolaft** (`Plataforma › Listas restrictivas (/plataforma/listas)`): Es la cuenta con la que consultan todas las empresas; quien no la tenga configurada no puede consultar Infolaft.

## Paso 2. Escribe usuario, clave, plan y cuota

La clave se cifra con la clave maestra y no se vuelve a mostrar: el campo queda vacío después de guardar. La cuota mensual es el número de búsquedas que cada empresa puede hacer al mes; al llegar a ella, la empresa no consulta más ese mes. El plan contratado es un texto informativo para quien administra la cuenta.

> **Pantalla:** `/plataforma/listas`

![Paso 2: Escribe usuario, clave, plan y cuota (escritorio)](../../../salida/manual/img/plataforma/listas-restrictivas-de-la-plataforma/02-escribe-usuario-clave-plan-y-cuota-claro.webp)

**Variantes**

- **Cuota vacía:** Cada empresa consulta sin tope mensual.
- **Se modifica la cuenta y se deja la clave vacía:** Se conserva la clave actual; «Vacío conserva la actual».

## Paso 3. Guarda la cuenta y comprueba que quedó configurada

Al guardar, la cuenta aparece como «Configurada» con los últimos cuatro caracteres de la clave, y el campo de clave se vacía. Ninguna empresa ve este panel: solo Plataforma. Quitar la cuenta deja a todas las empresas sin poder consultar Infolaft hasta que se configure otra.

> **Pantalla:** `/plataforma/listas`

![Paso 3: Guarda la cuenta y comprueba que quedó configurada (escritorio)](../../../salida/manual/img/plataforma/listas-restrictivas-de-la-plataforma/03-guarda-la-cuenta-y-comprueba-que-quedo-configura-claro.webp)

**Configuración que lo afecta**

- **Quitar la cuenta de Infolaft**: Ninguna empresa podrá consultar listas con Infolaft hasta que se configure otra cuenta.

## Paso 4. Así la ve una empresa del grupo

La empresa ve Infolaft como «Disponible» con su cuota mensual, y nada más: no pide usuario ni clave ni muestra la cuenta de la plataforma. Por eso no hay nada que configurar en cada empresa para consultar listas.

> **Pantalla:** `/e/{empresa}/listas-restrictivas`

![Paso 4: Así la ve una empresa del grupo (escritorio)](../../../salida/manual/img/plataforma/listas-restrictivas-de-la-plataforma/04-asi-la-ve-una-empresa-del-grupo-claro.webp)

**Variantes**

- **La plataforma no ha configurado la cuenta:** La empresa ve Infolaft como «No disponible» y no puede consultar con ella.

## Recorridos relacionados

- [Revisa los secretos en reposo y la clave de arranque](secretos.md)
- [Configura un tipo de documento y publica un flujo que lo pide](../configuracion/tipo-de-documento-y-flujo.md)
