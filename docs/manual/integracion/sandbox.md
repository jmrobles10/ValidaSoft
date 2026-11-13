---
id: "integracion/sandbox"
titulo: "Prueba la integración en el sandbox antes de producción"
actor: "Sistema integrador"
dominio: "integracion"
resumen: "El sistema integrador emite una llave de prueba, que crea el sandbox del grupo: una copia de sus empresas aislada de los datos reales. Ahí invita a un proveedor sin que salga ningún correo real, revisa el buzón de pruebas y, al final, reinicia el sandbox para empezar de cero."
pasos: 5
---

# Prueba la integración en el sandbox antes de producción

**Actor:** Sistema integrador · **Dominio:** Integración

El sistema integrador emite una llave de prueba, que crea el sandbox del grupo: una copia de sus empresas aislada de los datos reales. Ahí invita a un proveedor sin que salga ningún correo real, revisa el buzón de pruebas y, al final, reinicia el sandbox para empezar de cero.

**Antes de empezar**

- Un grupo con Constructora Norte, su flujo de proveedor publicado y el sistema integrado E2E registrado.
- Una administradora que puede emitir llaves de la API.

**Pasos**

1. Emite una llave de prueba
2. Revisa qué copió el sandbox
3. Invita a un proveedor de prueba
4. Revisa el buzón de pruebas
5. Reinicia el sandbox para empezar de cero

## Paso 1. Emite una llave de prueba

Una llave marcada como «sandbox» es la que usa tu sistema para probar. Su clave empieza por vsk_test_, así que se distingue a simple vista de una real. Al emitirla se crea el sandbox del grupo: una copia de cada empresa con datos de prueba, aislada de producción. Lleva los mismos permisos que llevaría la llave real, para que la prueba sea fiel.

![Paso 1: Emite una llave de prueba (escritorio)](../../../salida/manual/img/integracion/sandbox/01-emite-una-llave-de-prueba-claro.webp)

**Configuración que lo afecta**

- **Llave de sandbox (pruebas)** (`Configuración › API keys (/api-keys)`): Opera en un entorno aislado: sin datos reales y con los correos retenidos en un buzón.

## Paso 2. Revisa qué copió el sandbox

La respuesta dice si el sandbox existe, su entorno («sandbox», distinto de «produccion»), los sistemas que lo usan y las empresas copiadas. Cada empresa trae el id de su copia de pruebas (ese id es el que usas en X-Empresa), el id de producción del que sale y su prefijo de folio, que empieza por T para distinguirla.

![Paso 2: Revisa qué copió el sandbox (escritorio)](../../../salida/manual/img/integracion/sandbox/02-revisa-que-copio-el-sandbox-claro.webp)

**Variantes**

- **Consultas el mismo estado con una llave de producción:** Responde que el entorno es «produccion» y no ve el sandbox.

## Paso 3. Invita a un proveedor de prueba

La invitación se crea igual que en producción, con la misma API y la empresa de prueba en X-Empresa. La diferencia es el destino: el correo no sale a una persona real, queda guardado en el buzón de pruebas del sandbox, donde tu equipo puede abrir el enlace y completar la invitación como lo haría el proveedor.

![Paso 3: Invita a un proveedor de prueba (escritorio)](../../../salida/manual/img/integracion/sandbox/03-invita-a-un-proveedor-de-prueba-claro.webp)

## Paso 4. Revisa el buzón de pruebas

Cada correo que el sandbox habría enviado queda en el buzón, con su asunto y su destinatario. Así compruebas el correo de invitación sin que nadie reciba nada. Desde la API puedes leer cada correo por su id y vaciar el buzón cuando lo necesites.

![Paso 4: Revisa el buzón de pruebas (escritorio)](../../../salida/manual/img/integracion/sandbox/04-revisa-el-buzon-de-pruebas-claro.webp)

## Paso 5. Reinicia el sandbox para empezar de cero

Reiniciar borra los vínculos, los correos y los datos de prueba, pero conserva las mismas empresas y la misma llave. Producción no se entera: ni el vínculo, ni el certificado ni el correo existen allí. Úsalo cuando quieras repetir la prueba desde el principio.

![Paso 5: Reinicia el sandbox para empezar de cero (escritorio)](../../../salida/manual/img/integracion/sandbox/05-reinicia-el-sandbox-para-empezar-de-cero-claro.webp)

## Recorridos relacionados

- [Lee y vacía el buzón de correo de pruebas](buzon-de-pruebas.md)
- [Crea una API key con sus permisos y empresas, rótala y revócala](api-keys.md)
- [Crea un webhook, guarda su secreto y revisa las entregas firmadas](webhooks.md)
