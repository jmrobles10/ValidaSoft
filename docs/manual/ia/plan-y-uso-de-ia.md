---
id: "ia/plan-y-uso-de-ia"
titulo: "Revisa el plan y el uso de IA de la empresa"
actor: "Administrador de empresa"
dominio: "ia"
resumen: "La administradora consulta «Plan y uso de IA»: cuántas validaciones automáticas incluye el plan, cuántas ha usado este mes, si tiene validaciones adicionales y cuánto consumió. Esta pantalla no la ve el tercero."
pasos: 2
---

# Revisa el plan y el uso de IA de la empresa

**Actor:** Administrador de empresa · **Dominio:** IA y revisión automática

La administradora consulta «Plan y uso de IA»: cuántas validaciones automáticas incluye el plan, cuántas ha usado este mes, si tiene validaciones adicionales y cuánto consumió. Esta pantalla no la ve el tercero.

**Antes de empezar**

- Un grupo con Constructora Sur y el plan de IA contratado para la empresa, con 20 validaciones incluidas al mes y sin adicional. El proveedor de IA es simulado.

**Pasos**

1. Abre Plan y uso de IA
2. Revisa cómo se cuenta una validación

## Paso 1. Abre Plan y uso de IA

La pantalla muestra el plan de IA de la empresa para el mes elegido. «Validaciones incluidas» cuenta cuántos documentos se analizaron con el cupo del plan (por ejemplo, «0 de 20»), y debajo dice cuántas quedan. «Validaciones adicionales» dice si el plan permite comprar más cuando se agota el cupo.

> **Pantalla:** `/e/{empresa}/uso-ia`

![Paso 1: Abre Plan y uso de IA (escritorio)](../../../salida/manual/img/ia/plan-y-uso-de-ia/01-abre-plan-y-uso-de-ia-claro.webp)

**Variantes**

- **La empresa no tiene plan de IA:** La pantalla dice «La revisión automática no está contratada»: sus documentos se revisan manualmente.
- **Se agotaron las incluidas y no hay adicional:** Los documentos nuevos pasan a revisión manual, sin análisis automático.

**Configuración que lo afecta**

- **Plan de IA de la empresa** (`Configuración › Plan y uso de IA (/uso-ia)`): El plan lo configura Validasoft desde Plataforma; aquí solo se consulta el uso.

## Paso 2. Revisa cómo se cuenta una validación

Una validación es el análisis automático de un documento que recibe la empresa. Si otra empresa ya pagó el análisis de ese mismo documento, igual cuenta una validación de tu cupo. Los administradores reciben un aviso al llegar al 80 % y al 100 % de las incluidas.

> **Pantalla:** `/e/{empresa}/uso-ia`

![Paso 2: Revisa cómo se cuenta una validación (escritorio)](../../../salida/manual/img/ia/plan-y-uso-de-ia/02-revisa-como-se-cuenta-una-validacion-claro.webp)

## Recorridos relacionados

- [Elige el nivel de revisión automática con IA](revision-automatica-niveles.md)
- [Atiende los documentos cuando se agota el cupo de IA](cupo-agotado.md)
