---
id: "vinculos/cuenta-designada-aprobar"
titulo: "Aprueba la cuenta bancaria designada de un proveedor"
actor: "Oficial de cumplimiento"
dominio: "vinculos"
resumen: "Las dos áreas aprueban el expediente de un proveedor, pero no se cierra: el tipo de vínculo exige una cuenta bancaria designada y aprobada. La oficial de cumplimiento coteja la cuenta con el número completo y la aprueba; con la segunda aprobación de otra persona, la cuenta queda designada y el expediente se aprueba solo."
pasos: 5
---

# Aprueba la cuenta bancaria designada de un proveedor

**Actor:** Oficial de cumplimiento · **Dominio:** Vínculos y terceros

Las dos áreas aprueban el expediente de un proveedor, pero no se cierra: el tipo de vínculo exige una cuenta bancaria designada y aprobada. La oficial de cumplimiento coteja la cuenta con el número completo y la aprueba; con la segunda aprobación de otra persona, la cuenta queda designada y el expediente se aprueba solo.

**Antes de empezar**

- Un grupo con Constructora Norte, una oficial de cumplimiento, un revisor de Compras y una administradora, y el tipo de vínculo Proveedor con «Exige cuenta bancaria designada» activado.
- Un proveedor que completó su invitación, registró una cuenta bancaria con certificación y la designó para Norte, y el área Compras ya aprobó su parte.

**Pasos**

1. Aprueba tu área: el expediente espera la cuenta
2. Revisa la cuenta que el proveedor quiere designar
3. Aprueba la cuenta
4. La segunda aprobación designa la cuenta
5. El expediente se aprueba solo

## Paso 1. Aprueba tu área: el expediente espera la cuenta

Compras ya había aprobado y ahora aprobaste Cumplimiento, la última área. Aun así el expediente no se cierra: sigue «En revisión» porque el tipo de vínculo Proveedor exige una cuenta bancaria designada y aprobada, y todavía no lo está. El aviso «Falta la cuenta bancaria designada» lo dice, y «Ver la cuenta» te lleva al vínculo, donde dos personas de la empresa aprueban la cuenta que designó el proveedor. Rechazar esa cuenta no rechaza el expediente: sigue esperando a que el proveedor designe otra.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Aprueba tu área: el expediente espera la cuenta (escritorio)](../../../salida/manual/img/vinculos/cuenta-designada-aprobar/01-aprueba-tu-area-el-expediente-espera-la-cuenta-claro.webp)

**Variantes**

- **El tipo de vínculo no exige cuenta designada:** Al aprobar la última área el expediente se aprueba al instante: no aparece este aviso y la cuenta no frena el cierre.
- **El vínculo ya tenía una cuenta designada aprobada cuando se abrió el expediente:** La cuenta ya cuenta como cumplida: el expediente se aprueba apenas decide la última área.

**Configuración que lo afecta**

- **Exige cuenta bancaria designada (tipo de vínculo)** (`Configuración › Tipos de vínculo (/tipos-vinculo)`): Con la exigencia activa, el expediente no se aprueba hasta que la cuenta designada del vínculo esté aprobada, aunque todas las áreas hayan aprobado.

## Paso 2. Revisa la cuenta que el proveedor quiere designar

En el detalle del vínculo, la tarjeta «Cuenta bancaria designada» muestra la cuenta por aprobar con su banco, tipo y número enmascarado, y cuántas aprobaciones lleva. Antes de aprobar, coteja el número completo con la certificación bancaria del proveedor. Aquí llegas desde el aviso del expediente, pero también puedes abrir el vínculo desde la lista de vínculos.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Revisa la cuenta que el proveedor quiere designar (escritorio)](../../../salida/manual/img/vinculos/cuenta-designada-aprobar/02-revisa-la-cuenta-que-el-proveedor-quiere-designa-claro.webp)

**Variantes**

- **El tipo de vínculo no exige cuenta designada:** La tarjeta no aparece en el detalle y el vínculo no necesita cuenta para avanzar.

**Configuración que lo afecta**

- **Exige cuenta bancaria designada (tipo de vínculo)** (`Configuración › Tipos de vínculo (/tipos-vinculo)`): Si el tipo la exige, el expediente del vínculo necesita una cuenta designada aprobada para cerrarse.

## Paso 3. Aprueba la cuenta

Tu aprobación cuenta como la primera de dos. La misma persona no cuenta dos veces: la tarjeta dice que falta la aprobación de otra persona. Si la cuenta no es correcta, puedes rechazarla con un motivo: el proveedor lo ve y la designada anterior no cambia.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Aprueba la cuenta (escritorio)](../../../salida/manual/img/vinculos/cuenta-designada-aprobar/03-aprueba-la-cuenta-claro.webp)

## Paso 4. La segunda aprobación designa la cuenta

Con la segunda aprobación de otra persona de la empresa, la cuenta queda designada y la tarjeta muestra quién aprobó. La cuenta nueva reemplaza a la anterior.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 4: La segunda aprobación designa la cuenta (escritorio)](../../../salida/manual/img/vinculos/cuenta-designada-aprobar/04-la-segunda-aprobacion-designa-la-cuenta-claro.webp)

## Paso 5. El expediente se aprueba solo

En el mismo momento en que la segunda persona aprueba la cuenta, el expediente que esperaba se cierra: queda «Expediente aprobado», el vínculo pasa a activo, se emite el certificado y el proveedor recibe el correo de aprobación. Nadie tiene que volver a pulsar «aprobar». El historial deja constancia: «Exige cuenta bancaria designada» cuando el expediente se abrió y «Cuenta bancaria designada aprobada» cuando se cumplió la exigencia.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 5: El expediente se aprueba solo (escritorio)](../../../salida/manual/img/vinculos/cuenta-designada-aprobar/05-el-expediente-se-aprueba-solo-claro.webp)

## Recorridos relacionados

- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
- [Crea un tipo de vínculo y lo desactiva](../configuracion/tipos-de-vinculo.md)
- [Designa la cuenta en la que te paga una empresa](../tercero/cuentas-bancarias.md)
