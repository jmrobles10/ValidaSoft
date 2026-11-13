---
id: "ia/cruce-de-la-ficha-con-los-soportes"
titulo: "Compara la información del tercero con su RUT"
actor: "Revisor/área"
dominio: "ia"
resumen: "Cuando el cliente llena su información general y sube el RUT, la IA lee el documento y cruza sus datos, campo por campo, con lo que el tercero declaró. Sofía, de Revisión, ve el cruce y los soportes antes de aprobar el área."
pasos: 2
---

# Compara la información del tercero con su RUT

**Actor:** Revisor/área · **Dominio:** IA y revisión automática

Cuando el cliente llena su información general y sube el RUT, la IA lee el documento y cruza sus datos, campo por campo, con lo que el tercero declaró. Sofía, de Revisión, ve el cruce y los soportes antes de aprobar el área.

**Antes de empezar**

- Un grupo con Constructora Sur, la IA activa y un flujo de Cliente que pide el RUT y la información general.

**Pasos**

1. Revisa el cruce con el documento
2. Confirma qué soporta la información

## Paso 1. Revisa el cruce con el documento

La tabla compara cada dato que declaró el tercero en su información general con el que leyó la IA en el RUT: razón social, NIT, dirección, ciudad, actividad económica, entre otros. Cada fila dice «Coincide», «Difiere» o «Sin verificar», si el documento no trae ese dato. El resumen cuenta coincidencias y diferencias, y la confianza de la lectura va arriba. El cruce no aprueba ni observa nada por sí mismo: la decisión es de quien revisa.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Revisa el cruce con el documento (escritorio)](../../../salida/manual/img/ia/cruce-de-la-ficha-con-los-soportes/01-revisa-el-cruce-con-el-documento-claro.webp)

## Paso 2. Confirma qué soporta la información

En «Soporte de la información» se ve con qué versión de la información general y de cada documento se decidió. Así queda claro que el cruce se hizo contra los datos del tercero que están vigentes.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Confirma qué soporta la información (escritorio)](../../../salida/manual/img/ia/cruce-de-la-ficha-con-los-soportes/02-confirma-que-soporta-la-informacion-claro.webp)

## Recorridos relacionados

- [Lee el análisis automático de un documento](analisis-de-un-documento.md)
- [Revisa un documento que el sistema aprobó solo](revision-automatica-aprueba-sola.md)
