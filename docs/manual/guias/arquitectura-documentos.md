---
titulo: "Almacenamiento de documentos"
seccion: "Arquitectura"
resumen: "Cómo se guardan los archivos: buckets y claves por SHA-256, subida en tres pasos con URL prefirmada, verificación, versiones inmutables, compartición, descargas auditadas y retención."
---

# Almacenamiento de documentos

Cómo se guardan los archivos: buckets y claves por SHA-256, subida en tres pasos con URL prefirmada, verificación, versiones inmutables, compartición, descargas auditadas y retención.

Un documento del tercero se **sube una sola vez** a su bóveda y cada empresa que lo exige lo ve sin copiarlo. Esa regla (ADR-0004 y ADR-0005) decide casi todo el almacenamiento: los binarios son inmutables, se identifican por su SHA-256 y el API nunca los transporta.

## Dónde y con qué clave

| Zona | Bucket | Clave |
|---|---|---|
| Documentos globales del tercero | `boveda` | `terceros/{terceroId}/{sha256}` |
| Documentos propios de un vínculo | `tenant-{grupo}` (se crea con la primera carga) | `empresas/{empresaId}/vinculos/{vinculoId}/{sha256}` |
| Subida en curso | el del destino | `cargas/{cargaId}` (se borra al confirmar) |
| Versión devuelta | el del destino | `cuarentena/{documentoId}/v{n}` (nunca se comparte) |
| Plantillas de un tipo de documento | `tenant-{grupo}` | `plantillas/{tipoId}/{sha256}` |
| PDF de un certificado | `tenant-{grupo}` | `empresas/{empresaId}/certificados/{folio}/{sha256}` |
| Evidencia de listas y adjuntos del oficial | `tenant-{grupo}` | `empresas/{empresaId}/verificaciones/{sha256}.{ext}` y `…/cumplimiento/{sha256}` |
| Exportación de la bóveda del tercero | `boveda` | `exportaciones/{terceroId}/{exportacionId}.zip` |

La clave **nunca lleva el nombre original ni la extensión**: el nombre va en el metadato del objeto (`nombre-original`) y el tipo real en el `Content-Type`. No hay un bucket por empresa: el aislamiento entre empresas del grupo es lógico (el `empresa_id` está en la clave y en la autorización).

## Subir un documento: tres pasos

1. **Declarar.** `POST /public/v1/boveda/documentos` con nombre, tamaño, tipo y SHA-256 (y los datos del formulario si el tipo los tiene). Responde 201 con la **URL prefirmada** de subida (15 minutos) y reserva el número de la próxima versión; si ese SHA-256 ya es una versión, responde 200 `duplicado`. Aquí se rechazan lo evidente: tamaño sobre el máximo del tipo (413), tipo no admitido (415) y los límites del plan del grupo para un documento propio.
2. **Subir.** El navegador envía el archivo **directo al almacén** con la URL prefirmada. El API no ve los bytes en tránsito.
3. **Confirmar.** `POST /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/confirmar`. El servidor lee el objeto (cortando si pasa del máximo), calcula su **SHA-256** y lo compara con el declarado (`hash-no-coincide`), inspecciona el contenido (**tipo real** por sus primeros bytes, número de páginas y **contenido activo** de un PDF: JavaScript, acciones automáticas, lanzadores, formularios que envían datos, adjuntos) y lo copia a su clave definitiva con el tipo real.

El resultado es síncrono. Un PDF con contenido activo o con más páginas que las del tipo queda **Devuelta** con su motivo (y va a `cuarentena/`); si no, queda **Actual**, la anterior pasa a **Superada** y la versión se **comparte** con los vínculos del tercero que exigen ese tipo. Cada vínculo que la recibe encola, por el outbox, adjuntarla a los requisitos de su expediente abierto y calcular su vigencia.

![Ciclo de vida de un documento: los tres pasos de la subida, los estados de una versión y los de su vigencia](diagramas/ciclo-documento.svg)

## Límites y formatos

| Regla | Valor |
|---|---|
| Tamaño por archivo | 20 MB por omisión (`Validasoft:Boveda:TamanoMaximoBytes`); cada tipo de documento puede bajarlo. |
| Páginas de un PDF | 50 por omisión (`MaximoPaginas`); cada tipo puede bajarlo. |
| Formatos | PDF, JPG, PNG y WebP, de la lista blanca; cada tipo restringe a un subconjunto (los canónicos RUT, Cámara de Comercio, Estados financieros y Declaración de renta, solo PDF). |
| Verificación del tipo | Se compara el tipo declarado con el real por sus primeros bytes; el tipo real es el que vale. |
| Antivirus | No hay un antivirus conectado: la versión queda con el hecho `NoDisponible` y pasa a Actual. Con `Validasoft:Boveda:ExigirAntivirus=true` espera en **En análisis** hasta que un antivirus la declare limpia. |
| URLs prefirmadas | Subida de 1 a 15 minutos (15 por omisión) y descarga de 1 a 15 (5 por omisión). |

## Versiones: inmutables y compartidas

- Una versión **nunca cambia**. Subir lo mismo (mismo SHA-256 y mismos datos) no crea otra: se devuelve la existente y, si el tipo lo permite, se propone **ratificar**.
- Los formularios (un documento de solo datos) no tienen binario: su huella es la de los datos validados contra el esquema del tipo, y la comparación entre versiones es un diff de campos determinista.
- El tercero puede **retirar** una versión con un motivo; si era la actual, la superada más reciente vuelve a serlo y cada empresa que ya la había aprobado debe revalidar. Un vínculo bloqueado no recibe la versión nueva hasta que se desbloquea.
- La **compartición** es la única puerta de lectura de una empresa: nace al aceptar la invitación (o de forma automática cuando llega una versión nueva, o al importar) y se congela mientras el vínculo está bloqueado.
- La **IA** lee cada versión **una sola vez** (los PDF como archivo y los JPG y PNG como imagen; WebP no se analiza) y deja hechos que ven todas las empresas con compartición; paga una de las empresas con las que está compartida (primero la del vínculo de contexto) que tenga plan activo, llave y cupo, y cada empresa aplica después su propio nivel de revisión. Con la IA deshabilitada en la instalación, o si ninguna empresa puede pagar el análisis, la versión se revisa a mano.

## Descargas, auditoría y retención

- **Descargar** es siempre una URL prefirmada de lectura que emite un endpoint que antes comprueba la compartición o la propiedad: el tercero (`/public/v1/boveda/.../descarga`), una persona de la empresa (`/api/v1/vinculos/{id}/documentos/{documentoId}/versiones/{n}/descarga`), el certificado (`/api/v1/certificados/{folio}/descarga`) o el público (`/public/v1/verificar/{folio}/descarga`). **Cada emisión registra** quién, desde dónde y qué versión (`DocumentoAccedido`).
- **Habeas data:** una solicitud de supresión elimina lo que no está referenciado por una aceptación ni por un certificado y conserva con su base legal lo que la ley exige (por omisión 10 años); la versión suprimida pierde su binario, sus datos y su análisis.
- **Exportación:** el tercero puede pedir un ZIP de su bóveda (hasta 5 por día, vigente 7 días); la empresa puede exportar el expediente completo.

## Certificados verificables

El certificado de un vínculo aprobado es un documento con **contenido canónico** (JSON canonicalizado con RFC 8785), su **hash SHA-256** y un **sello Ed25519** de la plataforma. Su folio tiene la forma `{PREFIJO}-C-{año}-{secuencia de 6 dígitos}-{emisión}`; la **huella** (12 hexadecimales del hash) desambigua el folio entre grupos. El PDF lleva el folio, la URL de verificación, el hash, el sello y un QR; **no es PDF/A**: las fuentes estándar no se incrustan.

La verificación pública (`GET /public/v1/verificar/{folio}`) devuelve el estado, el titular, el emisor, el flujo aprobado y el contenido canónico, y la página `/verificar/{folio}` recalcula en el navegador el hash y comprueba el sello; las claves públicas con las que se sella están en `/.well-known/validasoft-certificados.json` (con las retiradas que aún no vencen, que solo valen para emisiones anteriores a su retiro, para verificar las viejas tras una rotación). El PDF solo se entrega de una emisión **Vigente** o «En verificación». Los certificados de un sandbox no se verifican en público.

El ciclo de vida de un certificado está en [Ciclos de vida](arquitectura-ciclos-de-vida.md#certificado).
