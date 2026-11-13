---
id: "integracion/invitar-por-api"
titulo: "Invita a un proveedor desde tu sistema con la API"
actor: "Sistema integrador"
dominio: "integracion"
resumen: "El ERP del cliente crea el vínculo con un POST a la API, con el tercero, su correo y su propia referencia. Validasoft envía la invitación y el vínculo aparece en la lista de la empresa, con esa referencia para buscarlo después."
pasos: 3
---

# Invita a un proveedor desde tu sistema con la API

**Actor:** Sistema integrador · **Dominio:** Integración

El ERP del cliente crea el vínculo con un POST a la API, con el tercero, su correo y su propia referencia. Validasoft envía la invitación y el vínculo aparece en la lista de la empresa, con esa referencia para buscarlo después.

**Antes de empezar**

- Un grupo con Constructora Norte, su flujo de proveedor publicado y la llave del sistema integrado con permiso para crear vínculos.

**Pasos**

1. Envía la invitación con un POST
2. Encuentra el vínculo creado en la lista
3. Busca el vínculo por tu referencia

## Paso 1. Envía la invitación con un POST

Tu sistema llama a POST /api/v1/vinculos con la llave de la API y la empresa en la cabecera X-Empresa. El cuerpo trae los datos del tercero, su tipo de vínculo, el correo al que va la invitación y tu referencia (sistema e identificador). La cabecera Idempotency-Key identifica la invitación: si tu sistema repite la misma llamada por un fallo de red, Validasoft no crea un segundo vínculo.

![Paso 1: Envía la invitación con un POST (escritorio)](../../../salida/manual/img/integracion/invitar-por-api/01-envia-la-invitacion-con-un-post-claro.webp)

**Variantes**

- **Mandas «enviarInvitacion» en verdadero:** Validasoft envía de inmediato el correo de invitación al contacto indicado.
- **Mandas «enviarInvitacion» en falso:** El vínculo se crea sin correo: la invitación se envía después desde la pantalla.

## Paso 2. Encuentra el vínculo creado en la lista

El vínculo aparece en la lista de Constructora Norte como cualquier otro, con el tercero y su correo. La columna «Referencias externas» muestra el sistema y tu identificador, así que el equipo de compras ve el mismo código que tiene en el ERP.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 2: Encuentra el vínculo creado en la lista (escritorio)](../../../salida/manual/img/integracion/invitar-por-api/02-encuentra-el-vinculo-creado-en-la-lista-claro.webp)

## Paso 3. Busca el vínculo por tu referencia

El buscador acepta la referencia que pusiste al crear el vínculo: escribe el identificador de tu sistema y la lista se reduce al vínculo que corresponde. Así tu sistema y la persona que revisa hablan del mismo proveedor sin copiar códigos a mano.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 3: Busca el vínculo por tu referencia (escritorio)](../../../salida/manual/img/integracion/invitar-por-api/03-busca-el-vinculo-por-tu-referencia-claro.webp)

## Recorridos relacionados

- [Localiza un vínculo por la referencia de un sistema externo](../vinculos/referencias-externas.md)
- [Crea una API key con sus permisos y empresas, rótala y revócala](api-keys.md)
