---
id: "documentos/vencimientos"
titulo: "Revisa los documentos que vencen en la lista de vencimientos"
actor: "Administrador de empresa"
dominio: "documentos"
resumen: "El administrador de Constructora Sur abre Vencimientos y ve, documento por documento, hasta cuándo sirve cada certificado que los proveedores le compartieron. Distingue lo vigente, lo que está por vencer, lo que está en gracia y lo vencido."
pasos: 2
---

# Revisa los documentos que vencen en la lista de vencimientos

**Actor:** Administrador de empresa · **Dominio:** Documentos y vigencias

El administrador de Constructora Sur abre Vencimientos y ve, documento por documento, hasta cuándo sirve cada certificado que los proveedores le compartieron. Distingue lo vigente, lo que está por vencer, lo que está en gracia y lo vencido.

**Antes de empezar**

- Un grupo con Constructora Sur, con el certificado de seguridad y salud en el trabajo como requisito del flujo de proveedor.
- Tres proveedores con el certificado expedido hace 3 días, 25 días y 40 días, para que estén vigentes, por vencer y vencidos.

**Pasos**

1. Abre la lista de vencimientos
2. Distingue los estados de vigencia

## Paso 1. Abre la lista de vencimientos

Vencimientos reúne los documentos que los terceros compartieron con tu empresa, con la fecha hasta la que están vigentes para ella. Cada fila muestra el tercero, el documento y su versión, la fecha «Vigente hasta», el estado y los días que faltan o que pasaron.

> **Pantalla:** `/e/{empresa}/vencimientos`

![Paso 1: Abre la lista de vencimientos (escritorio)](../../../salida/manual/img/documentos/vencimientos/01-abre-la-lista-de-vencimientos-claro.webp)

**Configuración que lo afecta**

- **Vigencia del tipo de documento** (`Configuración › Tipos de documento (/tipos-documento)`): La regla de vigencia de cada tipo (desde la expedición, la carga o un vencimiento propio) decide la fecha «Vigente hasta».

## Paso 2. Distingue los estados de vigencia

Vigente (1): falta más de la anticipación de aviso. Por vencer (2): el documento sigue sirviendo, pero le quedan pocos días. En gracia: ya venció, pero el tercero aún puede subir una versión nueva hasta la fecha que se indica. Vencida (3): pasó el periodo de gracia y el tercero debe subir una versión nueva.

> **Pantalla:** `/e/{empresa}/vencimientos`

![Paso 2: Distingue los estados de vigencia (escritorio)](../../../salida/manual/img/documentos/vencimientos/02-distingue-los-estados-de-vigencia-claro.webp)

## Recorridos relacionados

- [Sigue el ciclo de vigencia de un certificado](ciclo-de-vigencia.md)
- [Recuerda a un tercero un documento que vence](recordatorio-desde-vencimientos.md)
