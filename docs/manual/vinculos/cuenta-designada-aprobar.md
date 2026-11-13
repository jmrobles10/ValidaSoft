---
id: "vinculos/cuenta-designada-aprobar"
titulo: "Aprueba la cuenta bancaria designada de un proveedor"
actor: "Oficial de cumplimiento"
dominio: "vinculos"
resumen: "La oficial de cumplimiento revisa la cuenta bancaria que un proveedor designó para pagos de Constructora Norte, la coteja con el número completo y la aprueba. La cuenta solo queda designada cuando la aprueban dos personas distintas."
pasos: 3
---

# Aprueba la cuenta bancaria designada de un proveedor

**Actor:** Oficial de cumplimiento · **Dominio:** Vínculos y terceros

La oficial de cumplimiento revisa la cuenta bancaria que un proveedor designó para pagos de Constructora Norte, la coteja con el número completo y la aprueba. La cuenta solo queda designada cuando la aprueban dos personas distintas.

**Antes de empezar**

- Un grupo con Constructora Norte y una oficial de cumplimiento, y un tipo de proveedor que exige cuenta designada.
- Un proveedor que completó su invitación, registró una cuenta bancaria con certificación y la designó para Norte.

**Pasos**

1. Revisa la cuenta que el proveedor quiere designar
2. Aprueba la cuenta
3. La segunda aprobación designa la cuenta

## Paso 1. Revisa la cuenta que el proveedor quiere designar

En el detalle del vínculo, la tarjeta «Cuenta bancaria designada» muestra la cuenta por aprobar con su banco, tipo y número enmascarado, y cuántas aprobaciones lleva. Antes de aprobar, coteja el número completo con la certificación bancaria del proveedor.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Revisa la cuenta que el proveedor quiere designar (escritorio)](../../../salida/manual/img/vinculos/cuenta-designada-aprobar/01-revisa-la-cuenta-que-el-proveedor-quiere-designa-claro.webp)

**Variantes**

- **El tipo de vínculo no exige cuenta designada:** La tarjeta no aparece en el detalle y el vínculo no necesita cuenta para avanzar.

**Configuración que lo afecta**

- **Exige cuenta bancaria designada (tipo de vínculo)** (`Configuración › Tipos de vínculo (/tipos-vinculo)`): Si el tipo la exige, el cierre del vínculo necesita una cuenta designada aprobada.

## Paso 2. Aprueba la cuenta

Tu aprobación cuenta como la primera de dos. La misma persona no cuenta dos veces: la tarjeta dice que falta la aprobación de otra persona. Si la cuenta no es correcta, puedes rechazarla con un motivo: el proveedor lo ve y la designada anterior no cambia.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Aprueba la cuenta (escritorio)](../../../salida/manual/img/vinculos/cuenta-designada-aprobar/02-aprueba-la-cuenta-claro.webp)

## Paso 3. La segunda aprobación designa la cuenta

Con la segunda aprobación de otra persona de la empresa, la cuenta queda designada y la tarjeta muestra quién aprobó. La cuenta nueva reemplaza a la anterior.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: La segunda aprobación designa la cuenta (escritorio)](../../../salida/manual/img/vinculos/cuenta-designada-aprobar/03-la-segunda-aprobacion-designa-la-cuenta-claro.webp)

## Recorridos relacionados

- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
- [Configura un tipo de documento y publica un flujo que lo pide](../configuracion/tipo-de-documento-y-flujo.md)
