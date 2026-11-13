---
id: "documentos/ciclo-de-vigencia"
titulo: "Sigue el ciclo de vigencia de un certificado"
actor: "Administrador de empresa"
dominio: "documentos"
resumen: "El administrador de Constructora Sur ve los cuatro momentos por los que pasa un certificado de seguridad y salud en el trabajo: vigente, por vencer, en periodo de gracia y vencido. Cada uno cambia la fila de Vencimientos y lo que el proveedor puede hacer."
pasos: 4
---

# Sigue el ciclo de vigencia de un certificado

**Actor:** Administrador de empresa · **Dominio:** Documentos y vigencias

El administrador de Constructora Sur ve los cuatro momentos por los que pasa un certificado de seguridad y salud en el trabajo: vigente, por vencer, en periodo de gracia y vencido. Cada uno cambia la fila de Vencimientos y lo que el proveedor puede hacer.

**Antes de empezar**

- Un grupo con Constructora Sur, con el certificado de SST como requisito del flujo de proveedor (30 días de vigencia desde la expedición, 10 de aviso y 5 de gracia).
- Cuatro proveedores con el certificado expedido hace 3, 25, 32 y 40 días, uno en cada estado.

**Pasos**

1. Mira el certificado vigente
2. Entra en aviso: por vencer
3. Vence la vigencia: en gracia
4. Pasa la gracia: vencida

## Paso 1. Mira el certificado vigente

Con la expedición hace 3 días, el certificado está vigente: la fila muestra «Vigente hasta» y los días que faltan. Mientras falte más que el plazo de aviso, no hay nada que hacer.

> **Pantalla:** `/e/{empresa}/vencimientos`

![Paso 1: Mira el certificado vigente (escritorio)](../../../salida/manual/img/documentos/ciclo-de-vigencia/01-mira-el-certificado-vigente-claro.webp)

**Configuración que lo afecta**

- **Vigencia del tipo de documento** (`Configuración › Tipos de documento (/tipos-documento)`): Los días de vigencia, de aviso y de gracia de cada tipo deciden en qué estado queda la fila.

## Paso 2. Entra en aviso: por vencer

Con la expedición hace 25 días, faltan pocos días para el vencimiento y el certificado queda «Por vencer». Sigue sirviendo, pero la fila lo marca para que el proveedor lo renueve a tiempo.

> **Pantalla:** `/e/{empresa}/vencimientos`

![Paso 2: Entra en aviso: por vencer (escritorio)](../../../salida/manual/img/documentos/ciclo-de-vigencia/02-entra-en-aviso-por-vencer-claro.webp)

## Paso 3. Vence la vigencia: en gracia

Con la expedición hace 32 días, la vigencia ya venció, pero el periodo de gracia sigue abierto: la fila dice «En gracia» y «Se acepta hasta» una fecha. El proveedor aún puede subir una versión nueva hasta esa fecha.

> **Pantalla:** `/e/{empresa}/vencimientos`

![Paso 3: Vence la vigencia: en gracia (escritorio)](../../../salida/manual/img/documentos/ciclo-de-vigencia/03-vence-la-vigencia-en-gracia-claro.webp)

## Paso 4. Pasa la gracia: vencida

Con la expedición hace 40 días, pasó también el periodo de gracia y el certificado queda «Vencida». El proveedor recibe un aviso por correo de que el documento venció para tu empresa, y debe subir una versión nueva.

> **Pantalla:** `/e/{empresa}/vencimientos`

![Paso 4: Pasa la gracia: vencida (escritorio)](../../../salida/manual/img/documentos/ciclo-de-vigencia/04-pasa-la-gracia-vencida-claro.webp)

## Recorridos relacionados

- [Revisa los documentos que vencen en la lista de vencimientos](vencimientos.md)
- [Sigue la vigencia periódica de un vínculo](vigencia-del-vinculo.md)
