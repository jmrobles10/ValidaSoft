---
id: "integracion/api-keys"
titulo: "Crea una API key con sus permisos y empresas, rótala y revócala"
actor: "Administrador de empresa"
dominio: "integracion"
resumen: "La administradora emite una clave para que un sistema llame a la API: elige sus permisos y las empresas a las que alcanza, ve la clave una sola vez, la rota con un periodo de solapamiento y, al final, la revoca."
pasos: 11
---

# Crea una API key con sus permisos y empresas, rótala y revócala

**Actor:** Administrador de empresa · **Dominio:** Integración

La administradora emite una clave para que un sistema llame a la API: elige sus permisos y las empresas a las que alcanza, ve la clave una sola vez, la rota con un periodo de solapamiento y, al final, la revoca.

**Antes de empezar**

- Un grupo empresarial con dos empresas, el sistema integrado E2E registrado y una administradora que puede administrar API keys.

**Pasos**

1. Abre Configuración › API keys
2. Pon un nombre y el sistema que la usará
3. Elige los permisos y las empresas de alcance
4. Revisa las opciones de expiración y sandbox
5. Copia la clave: solo se muestra una vez
6. Revisa la clave en la tabla
7. Rota la clave con solapamiento
8. Lee hasta cuándo vale la clave anterior
9. Mira la clave anterior como reemplazada
10. Revoca una clave
11. Confirma que la clave quedó revocada

## Paso 1. Abre Configuración › API keys

La tabla lista las claves del grupo: su nombre, la clave enmascarada, los permisos que tienen, las empresas de alcance, el sistema al que pertenecen, cuándo expiran y su estado (vigente, vencida o reemplazada). Una clave nunca se vuelve a mostrar completa: lo que ves es solo su final. Para crear una, pulsa «Nueva API key».

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 1: Abre Configuración › API keys (escritorio)](../../../salida/manual/img/integracion/api-keys/01-abre-configuracion-api-keys-claro.webp)

**Configuración que lo afecta**

- **API keys** (`Configuración › API keys (/api-keys)`): Cada clave autentica las llamadas a la API con los permisos y en las empresas que se le dieron.

## Paso 2. Pon un nombre y el sistema que la usará

El nombre sirve para reconocerla después: indica el sistema y el ambiente, por ejemplo «ERP producción». Si la clave es la llave de un sistema integrado registrado, elígelo: al desactivar ese sistema, la clave deja de autenticar. Sin sistema, la clave es de uso general.

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 2: Pon un nombre y el sistema que la usará (escritorio)](../../../salida/manual/img/integracion/api-keys/02-pon-un-nombre-y-el-sistema-que-la-usara-claro.webp)

**Variantes**

- **Eliges un sistema integrado:** La clave queda ligada a ese sistema y se desactiva con él.
- **No eliges sistema:** La clave no depende de ningún sistema integrado.

## Paso 3. Elige los permisos y las empresas de alcance

Marca solo lo que el sistema necesita: cada permiso habilita un tipo de llamada, y la lista dice cuántos marcaste. Después elige en qué empresas puede operar: «Todo el grupo» incluye también las que se agreguen después; «Solo algunas empresas» limita la clave a las que marques.

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 3: Elige los permisos y las empresas de alcance (escritorio)](../../../salida/manual/img/integracion/api-keys/03-elige-los-permisos-y-las-empresas-de-alcance-claro.webp)

**Variantes**

- **Eliges «Todo el grupo»:** La clave alcanza también las empresas que se creen después en el grupo.
- **Eliges «Solo algunas empresas»:** La clave solo opera en las empresas marcadas.

## Paso 4. Revisa las opciones de expiración y sandbox

La expiración es opcional: vacía, la clave no expira; si pones una fecha, vale hasta el final de ese día. La llave de sandbox opera en un entorno de pruebas aislado, sin datos reales y con los correos retenidos en un buzón, así que sirve para probar la integración sin tocar al grupo.

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 4: Revisa las opciones de expiración y sandbox (escritorio)](../../../salida/manual/img/integracion/api-keys/04-revisa-las-opciones-de-expiracion-y-sandbox-claro.webp)

**Variantes**

- **Dejas la expiración vacía:** La clave no expira hasta que la rotes o la reveques.
- **Marcas «Llave de sandbox»:** Se crea en el entorno de pruebas del grupo, no en el real.

## Paso 5. Copia la clave: solo se muestra una vez

Al crear la clave, Validasoft la muestra completa una sola vez. Cópiala con «Copiar clave» y guárdala en el gestor de secretos del sistema que la va a usar. Ese sistema la envía en la cabecera X-Api-Key de cada llamada. Si la pierdes, no hay forma de recuperarla: crea otra y revoca esta.

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 5: Copia la clave: solo se muestra una vez (escritorio)](../../../salida/manual/img/integracion/api-keys/05-copia-la-clave-solo-se-muestra-una-vez-claro.webp)

## Paso 6. Revisa la clave en la tabla

La clave aparece como vigente, con sus permisos resumidos, su alcance y el sistema al que pertenece. Desde su fila puedes rotarla o revocarla. Una clave sin fecha de expiración se muestra como «No expira».

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 6: Revisa la clave en la tabla (escritorio)](../../../salida/manual/img/integracion/api-keys/06-revisa-la-clave-en-la-tabla-claro.webp)

## Paso 7. Rota la clave con solapamiento

Rotar emite una clave nueva con los mismos permisos, alcance y sistema, y la anterior sigue valiendo el tiempo que elijas: ahora mismo, una hora, 24 horas o siete días. Así cambias la clave en el sistema que la usa sin cortarle el servicio. Coordina el cambio antes de que la anterior deje de valer.

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 7: Rota la clave con solapamiento (escritorio)](../../../salida/manual/img/integracion/api-keys/07-rota-la-clave-con-solapamiento-claro.webp)

**Variantes**

- **Eliges «No: deja de valer ahora»:** La clave anterior deja de autenticar en ese momento; el sistema debe cambiarla al instante.
- **Eliges un solapamiento:** La anterior sigue valiendo hasta la hora que muestra la pantalla de la clave nueva.

## Paso 8. Lee hasta cuándo vale la clave anterior

La clave nueva se muestra una sola vez, como la primera. Además, la pantalla dice hasta qué hora vale la anterior: ese es el plazo para cambiarla en el sistema. Cuando pase esa hora, las llamadas con la clave anterior responden 401.

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 8: Lee hasta cuándo vale la clave anterior (escritorio)](../../../salida/manual/img/integracion/api-keys/08-lee-hasta-cuando-vale-la-clave-anterior-claro.webp)

## Paso 9. Mira la clave anterior como reemplazada

En la tabla, la clave anterior aparece como «Reemplazada» con la hora en que deja de valer, y la nueva como vigente. Mientras el plazo no pase, ambas autentican.

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 9: Mira la clave anterior como reemplazada (escritorio)](../../../salida/manual/img/integracion/api-keys/09-mira-la-clave-anterior-como-reemplazada-claro.webp)

## Paso 10. Revoca una clave

Revocar deja de autenticar la clave, como mucho cinco minutos después de confirmar. Si un sistema la usa, deja de funcionar hasta que le pongas otra clave. No se puede deshacer: la fila queda en la tabla como historial.

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 10: Revoca una clave (escritorio)](../../../salida/manual/img/integracion/api-keys/10-revoca-una-clave-claro.webp)

## Paso 11. Confirma que la clave quedó revocada

Validasoft confirma con un aviso que la clave quedó revocada, y su fila muestra el estado correspondiente. Desde ese momento, las llamadas con esa clave responden 401.

> **Pantalla:** `/e/{empresa}/api-keys`

![Paso 11: Confirma que la clave quedó revocada (escritorio)](../../../salida/manual/img/integracion/api-keys/11-confirma-que-la-clave-quedo-revocada-claro.webp)

## Recorridos relacionados

- [Registra un sistema integrado con su clave pública](sistema-integrador.md)
- [Invita a un proveedor desde tu sistema con la API](invitar-por-api.md)
- [Consulta la certificación de un proveedor con ETag](consulta-de-certificacion.md)
