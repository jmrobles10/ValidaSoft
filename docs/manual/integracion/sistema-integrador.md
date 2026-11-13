---
id: "integracion/sistema-integrador"
titulo: "Registra un sistema integrado con su clave pública"
actor: "Administrador de empresa"
dominio: "integracion"
resumen: "La administradora registra un sistema externo (un ERP, un portal) con su código, su nombre, su clave pública y los sitios desde los que se incrusta Validasoft. Desde ese momento las personas pueden entrar desde ese sistema."
pasos: 5
---

# Registra un sistema integrado con su clave pública

**Actor:** Administrador de empresa · **Dominio:** Integración

La administradora registra un sistema externo (un ERP, un portal) con su código, su nombre, su clave pública y los sitios desde los que se incrusta Validasoft. Desde ese momento las personas pueden entrar desde ese sistema.

**Antes de empezar**

- Un grupo empresarial con dos empresas y una administradora que puede administrar sistemas integrados.
- El sistema E2E ya registrado en el grupo (así se ve la tabla con datos).

**Pasos**

1. Abre Configuración › Sistemas integrados
2. Escribe el código y el nombre del sistema
3. Pega la clave pública del sistema
4. Indica desde qué sitios se incrusta Validasoft
5. Revisa el sistema registrado

## Paso 1. Abre Configuración › Sistemas integrados

La tabla lista los sistemas desde los que entran las personas del grupo y crean vínculos por API. Cada fila muestra el nombre y el código del sistema, si está activo, la huella de su clave pública, cuántos sitios pueden incrustar Validasoft y la fecha de registro. Validasoft solo guarda la clave pública: el sistema firma con su clave privada la identidad de sus usuarios, y esa clave privada nunca llega a Validasoft.

> **Pantalla:** `/e/{empresa}/sistemas`

![Paso 1: Abre Configuración › Sistemas integrados (escritorio)](../../../salida/manual/img/integracion/sistema-integrador/01-abre-configuracion-sistemas-integrados-claro.webp)

**Configuración que lo afecta**

- **Sistemas integrados** (`Configuración › Sistemas integrados (/sistemas)`): Un sistema registrado puede firmar identidades y, con sus API keys, llamar a la API del grupo.

## Paso 2. Escribe el código y el nombre del sistema

El código (iss) es el emisor de las aserciones que firma el sistema: Validasoft lo usa para saber de qué sistema llega cada identidad, así que no cambia después. Se escribe en mayúsculas, sin espacios, y el nombre es el que verás en la tabla. Mientras falte alguno, el botón «Registrar sistema» queda deshabilitado y te dice qué falta.

> **Pantalla:** `/e/{empresa}/sistemas`

![Paso 2: Escribe el código y el nombre del sistema (escritorio)](../../../salida/manual/img/integracion/sistema-integrador/02-escribe-el-codigo-y-el-nombre-del-sistema-claro.webp)

## Paso 3. Pega la clave pública del sistema

Pega la clave pública en formato PEM, la que empieza por «-----BEGIN PUBLIC KEY-----». Acepta RSA de 2048 bits o más, o EC P-256. Validasoft la verifica al guardar; una clave privada o un formato no válido se rechaza. Si el sistema debe rotarla luego, usa «Rotar clave» desde su ficha.

> **Pantalla:** `/e/{empresa}/sistemas`

![Paso 3: Pega la clave pública del sistema (escritorio)](../../../salida/manual/img/integracion/sistema-integrador/03-pega-la-clave-publica-del-sistema-claro.webp)

**Variantes**

- **La clave es RSA de 2048 bits o más, o EC P-256:** Se acepta y el sistema queda listo para firmar.
- **La clave es menor o de otro tipo:** Se rechaza con un mensaje; no se guarda nada.

## Paso 4. Indica desde qué sitios se incrusta Validasoft

Escribe un sitio por línea, con el protocolo y el dominio (y el puerto si aplica), sin ruta. Solo esos sitios pueden mostrar Validasoft dentro de un iframe y solo a ellos se les entrega la sesión. Es opcional: si lo dejas vacío, nadie puede incrustarlo todavía y puedes agregarlos después desde la ficha del sistema.

> **Pantalla:** `/e/{empresa}/sistemas`

![Paso 4: Indica desde qué sitios se incrusta Validasoft (escritorio)](../../../salida/manual/img/integracion/sistema-integrador/04-indica-desde-que-sitios-se-incrusta-validasoft-claro.webp)

**Variantes**

- **Escribes http://:** Solo se admite para localhost en desarrollo; en producción se rechaza.
- **Escribes una ruta o un comodín:** Se rechaza: el sitio es el origen, no una página.

## Paso 5. Revisa el sistema registrado

Al registrarlo, el sistema aparece en la tabla con su estado «Activo», la huella de su clave y el número de sitios. Su ficha muestra la clave pública completa, el emisor (iss) y la fecha de registro. Desde la ficha también puedes rotar la clave (la nueva vale solo para las aserciones firmadas con ella) o desactivar el sistema: sus API keys dejan de autenticar y las personas no pueden entrar desde él.

> **Pantalla:** `/e/{empresa}/sistemas`

![Paso 5: Revisa el sistema registrado (escritorio)](../../../salida/manual/img/integracion/sistema-integrador/05-revisa-el-sistema-registrado-claro.webp)

**Configuración que lo afecta**

- **Rotar clave y desactivar sistema** (`Configuración › Sistemas integrados (/sistemas)`): Rotar exige coordinar el cambio con el equipo del sistema.

## Recorridos relacionados

- [Crea una API key con sus permisos y empresas, rótala y revócala](api-keys.md)
- [Abre la app de tu empresa dentro de tu sistema](../embebido/app-de-la-empresa-en-un-iframe.md)
