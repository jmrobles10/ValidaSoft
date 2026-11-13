---
id: "operacion/plazo-de-cargue-y-caducidad"
titulo: "Atiende un expediente caducado por falta de plazo y reábrelo"
actor: "Administrador de empresa"
dominio: "operacion"
resumen: "El flujo da un plazo para completar el expediente. Cuando el tercero no lo completa a tiempo, el expediente caduca y vuelve a Invitado. La administradora lo reabre con un plazo nuevo y el tercero recibe un enlace nuevo."
pasos: 3
---

# Atiende un expediente caducado por falta de plazo y reábrelo

**Actor:** Administrador de empresa · **Dominio:** Operación masiva y continua

El flujo da un plazo para completar el expediente. Cuando el tercero no lo completa a tiempo, el expediente caduca y vuelve a Invitado. La administradora lo reabre con un plazo nuevo y el tercero recibe un enlace nuevo.

**Antes de empezar**

- Un grupo con Constructora Norte y un flujo de Proveedor con plazo de cargue de 30 días (el valor por omisión).
- Un tercero invitado que aceptó la invitación pero no ha subido documentos.

**Pasos**

1. Revisa el expediente caducado
2. Reabre el expediente con un plazo nuevo
3. Comprueba la reapertura

## Paso 1. Revisa el expediente caducado

Cuando el tercero no completa el expediente dentro del plazo de cargue, el expediente caduca: el aviso lo indica con la fecha en que venció el plazo. El vínculo vuelve a Invitado. Desde aquí puedes reabrir el expediente, que da al tercero un plazo nuevo completo y un enlace nuevo.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Revisa el expediente caducado (escritorio)](../../../salida/manual/img/operacion/plazo-de-cargue-y-caducidad/01-revisa-el-expediente-caducado-claro.webp)

**Variantes**

- **El flujo no tiene plazo de cargue:** El expediente no caduca y este aviso no aparece.

**Configuración que lo afecta**

- **Plazo de cargue** (`Configuración › Flujos aprobatorios (/flujos)`): Son los días que tiene el tercero para completar el expediente inicial. 0 significa sin plazo; el flujo también define los recordatorios antes de vencer.

## Paso 2. Reabre el expediente con un plazo nuevo

El diálogo explica que el expediente vuelve a Abierto con un plazo nuevo del tamaño configurado en el flujo, y que el tercero recibe un correo con un enlace para completarlo. La justificación es opcional y queda en el historial.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Reabre el expediente con un plazo nuevo (escritorio)](../../../salida/manual/img/operacion/plazo-de-cargue-y-caducidad/02-reabre-el-expediente-con-un-plazo-nuevo-claro.webp)

## Paso 3. Comprueba la reapertura

El aviso de caducidad desaparece y el expediente queda con el nuevo plazo indicado. El tercero recibe el correo de reapertura y ve de nuevo cuántos días le quedan para completarlo.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Comprueba la reapertura (escritorio)](../../../salida/manual/img/operacion/plazo-de-cargue-y-caducidad/03-comprueba-la-reapertura-claro.webp)

## Recorridos relacionados

- [Activa el recordatorio automático de observaciones](recordatorio-automatico-por-flujo.md)
- [Invita a varios terceros con un archivo](invitacion-por-lotes.md)
