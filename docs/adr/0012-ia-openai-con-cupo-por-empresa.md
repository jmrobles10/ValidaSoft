# ADR-0012 — IA con OpenAI y cupo de validaciones por empresa

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-11-09 (decisiones IA-1 a IA-7) | Cambia el proveedor de M-4 (Claude) y precisa docs/04 §IA |

## Contexto
La revisión automática (docs/02 §2.4, H7) necesita leer los documentos del tercero (RUT, Cámara de comercio, cédula, certificaciones…) y extraer sus datos con precisión. Producto decidió usar **OpenAI** por costo, con un modelo lo bastante barato para que el servicio sea rentable pero capaz de leer y analizar documentos con precisión. El servicio se vende por empresa: cada una tiene un número de validaciones incluidas al mes (del orden de US$20 de consumo de IA) y paga un adicional si quiere más. Validasoft debe controlar el uso de cada empresa por separado.

## Decisión
- **Proveedor y modelos.** OpenAI, API Responses con *structured outputs* (esquema JSON por tipo de documento) y el PDF como archivo de entrada (texto extraído más imágenes de página). Modelo por defecto `gpt-6-luna`; escalamiento a `gpt-6.1-terra` solo si la confianza queda bajo el umbral, si cambia un campo crítico o si el documento parece ilegible o alterado. Los modelos son configuración, no código.
- **Cliente = empresa** (razón social, ADR-0008). Cada empresa tiene su **plan de IA**: validaciones incluidas por mes, si tiene adicional habilitado (con tope opcional) y su **API key de OpenAI**, guardada cifrada. Lo configura Plataforma; la empresa ve su plan y su uso.
- **Unidad de cobro: la validación.** Una validación es el análisis de una versión de un documento **usado por una empresa**. El análisis se hace una sola vez por versión (hechos compartidos, P5) y se paga con la llave de la empresa que lo provocó; **cada empresa que lo usa consume una validación de su cupo**, aunque OpenAI se haya llamado una sola vez. Se mide además el costo real (tokens y dólares) en la empresa cuya llave pagó la llamada.
- **Sin cupo ni adicional**: esa empresa no recibe el análisis (sus documentos quedan «sin análisis automático» para revisión humana); nada se bloquea. Avisos al administrador al 80 % y al 100 %.
- **Límites por tipo de documento**: páginas máximas que se envían, nivel de detalle de las imágenes, tamaño y tokens de salida; lo que excede no se envía y queda anotado.
- **Fail-closed y reversibilidad**: cualquier error, tiempo agotado o respuesta inválida deja la versión para revisión humana; toda decisión automática guarda modelo, confianza, costo y evidencia, y se puede revertir.
- **Privacidad**: solo se analizan versiones compartidas con empresas cuyo tercero aceptó la autorización (ADR-0009); no se guarda el contenido enviado; se recomienda la retención cero del proveedor y el aviso de tratamiento nombra a OpenAI como encargado (P-6-5).

## Alternativas descartadas
- **Claude** (M-4 original): mejor precisión en algunos casos, pero más costoso para el volumen previsto.
- **Una llave de Validasoft para todos**: no permite aislar el costo de cada cliente ni cortar el gasto de uno sin afectar a los demás.
- **Cobrar solo a la empresa que provocó el análisis**: Producto prefiere que cada empresa que se beneficia de la validación la pague, como si fuera la única.

## Consecuencias
- Hace falta medición de uso por empresa y mes (validaciones, adicionales, tokens, costo) y la pantalla «Plan y uso».
- Un conjunto de prueba con documentos reales mide la precisión por campo antes de activar el modo automático (objetivo: ≥ 98 % en campos críticos).
- El SDK y las integraciones no cambian: la IA es interna y sus resultados viajan como hechos de la versión.
