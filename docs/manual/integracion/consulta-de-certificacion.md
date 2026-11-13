---
id: "integracion/consulta-de-certificacion"
titulo: "Consulta la certificación de un proveedor con ETag"
actor: "Sistema integrador"
dominio: "integracion"
resumen: "Un ERP consulta si un proveedor tiene certificado vigente. Guarda el ETag de la respuesta y la próxima vez lo manda en If-None-Match: si nada cambió, recibe 304 y se ahorra el cuerpo; si el vínculo se bloqueó, la respuesta trae la versión nueva."
pasos: 3
---

# Consulta la certificación de un proveedor con ETag

**Actor:** Sistema integrador · **Dominio:** Integración

Un ERP consulta si un proveedor tiene certificado vigente. Guarda el ETag de la respuesta y la próxima vez lo manda en If-None-Match: si nada cambió, recibe 304 y se ahorra el cuerpo; si el vínculo se bloqueó, la respuesta trae la versión nueva.

**Antes de empezar**

- Un grupo con Constructora Norte, un vínculo de proveedor ya certificado y una llave con permiso para consultar la certificación.

**Pasos**

1. Pide la certificación de un proveedor
2. Usa If-None-Match cuando no haya cambios
3. Recibe la versión nueva cuando algo cambió

## Paso 1. Pide la certificación de un proveedor

Con el tipo y número de identificación y el tipo de vínculo, la respuesta dice si el proveedor tiene certificado, su folio, su estado y si el vínculo está bloqueado. Además trae un ETag: una huella de esa versión que tu sistema guarda junto con la respuesta.

![Paso 1: Pide la certificación de un proveedor (escritorio)](../../../salida/manual/img/integracion/consulta-de-certificacion/01-pide-la-certificacion-de-un-proveedor-claro.webp)

**Variantes**

- **El proveedor no tiene vínculo con la empresa:** La respuesta dice «SinVinculo» y no inventa datos; no es un error.

## Paso 2. Usa If-None-Match cuando no haya cambios

Si mandas el ETag anterior en If-None-Match y la certificación no cambió, la API responde 304 sin cuerpo. Tu sistema conserva su copia y no transfiere datos de nuevo. Un 304 no significa que el proveedor no exista: significa que la versión que tienes sigue siendo la vigente.

![Paso 2: Usa If-None-Match cuando no haya cambios (escritorio)](../../../salida/manual/img/integracion/consulta-de-certificacion/02-usa-if-none-match-cuando-no-haya-cambios-claro.webp)

## Paso 3. Recibe la versión nueva cuando algo cambió

Cuando el vínculo se bloquea, la certificación cambia: la misma consulta, con el ETag anterior, responde 200 con los datos nuevos. Ahí aparece «bloqueado» en verdadero con su motivo, y el estado del vínculo pasa a «Bloqueado». Tu sistema debe reemplazar su copia y no seguir usando la anterior.

![Paso 3: Recibe la versión nueva cuando algo cambió (escritorio)](../../../salida/manual/img/integracion/consulta-de-certificacion/03-recibe-la-version-nueva-cuando-algo-cambio-claro.webp)

**Variantes**

- **El vínculo está bloqueado:** La respuesta no sirve para considerar al proveedor apto hasta que se desbloquee.

## Recorridos relacionados

- [Lee los eventos por pull o en vivo con un cursor](eventos-pull-y-en-vivo.md)
- [Crea una API key con sus permisos y empresas, rótala y revócala](api-keys.md)
