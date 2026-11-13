---
id: "integracion/importar-del-portal"
titulo: "Importa terceros desde el Portal de proveedores"
actor: "Plataforma"
dominio: "integracion"
resumen: "El operador de Plataforma lanza una importación desde el Portal de proveedores: primero una simulación que no escribe nada y luego la importación real. Al terminar, la corrida muestra cuántos terceros se crearon, cuántos ya existían y qué datos quedaron con advertencia o rechazados."
pasos: 4
---

# Importa terceros desde el Portal de proveedores

**Actor:** Plataforma · **Dominio:** Integración

El operador de Plataforma lanza una importación desde el Portal de proveedores: primero una simulación que no escribe nada y luego la importación real. Al terminar, la corrida muestra cuántos terceros se crearon, cuántos ya existían y qué datos quedaron con advertencia o rechazados.

**Antes de empezar**

- Un grupo con Constructora Norte, para mapear la unidad 7 del Portal a su empresa.
- La llave de Plataforma y un JSON de muestra con la forma del Portal (datos ficticios; no hay conexión real).

**Pasos**

1. Lanza una simulación desde el Portal
2. Revisa lo que haría la importación
3. Revisa el resultado de la importación real
4. Revisa los datos que no se pudieron importar

## Paso 1. Lanza una simulación desde el Portal

Con la llave de Plataforma envías el JSON del Portal a POST /api/v1/plataforma/importacion/portal. Con «dryRun» en verdadero la corrida solo calcula lo que haría: no crea ni cambia terceros. La respuesta es 202 con el identificador de la corrida, que se procesa en segundo plano por lotes de «tamanoLote» terceros.

![Paso 1: Lanza una simulación desde el Portal (escritorio)](../../../salida/manual/img/integracion/importar-del-portal/01-lanza-una-simulacion-desde-el-portal-claro.webp)

**Variantes**

- **«dryRun» está en falso:** La corrida escribe de verdad: crea los terceros nuevos y la versión importada de cada sección.
- **La fuente es «Conectado»:** El Portal se lee con la conexión configurada en el servidor; sin ella la API responde 409 «origen-no-configurado».

## Paso 2. Revisa lo que haría la importación

El resumen cuenta los terceros leídos, los que se crearían, los que ya existían y los rechazados. «aceptaciones» son las invitaciones aceptadas que se asocian a las empresas del mapa de unidades. Como es una simulación, la base de datos no cambió: puedes repetirla cuantas veces quieras antes de la importación real.

![Paso 2: Revisa lo que haría la importación (escritorio)](../../../salida/manual/img/integracion/importar-del-portal/02-revisa-lo-que-haria-la-importacion-claro.webp)

## Paso 3. Revisa el resultado de la importación real

Con «dryRun» en falso, los terceros nuevos se crean con su versión importada de cada sección, y los que ya existían por su número de identificación no se duplican. Si la corrida se interrumpe, se reanuda desde su último lote con POST …/{id}/reanudar, sin volver a procesar lo ya importado.

![Paso 3: Revisa el resultado de la importación real (escritorio)](../../../salida/manual/img/integracion/importar-del-portal/03-revisa-el-resultado-de-la-importacion-real-claro.webp)

**Variantes**

- **La corrida ya terminó:** Una corrida completada no vuelve a hacer nada: reanudarla responde 409 «corrida-completada».

## Paso 4. Revisa los datos que no se pudieron importar

Un dato con problema no detiene la corrida: el tercero queda rechazado con su motivo, o se importa con una advertencia (por ejemplo, un dígito de verificación que se recalculó). Cada aviso trae la clave del tercero en el Portal, el campo y el código del problema. El detalle completo se descarga como CSV desde …/{id}/rechazos.

![Paso 4: Revisa los datos que no se pudieron importar (escritorio)](../../../salida/manual/img/integracion/importar-del-portal/04-revisa-los-datos-que-no-se-pudieron-importar-claro.webp)

## Recorridos relacionados

- [Registra un sistema integrado con su clave pública](sistema-integrador.md)
- [Invita a un proveedor desde tu sistema con la API](invitar-por-api.md)
