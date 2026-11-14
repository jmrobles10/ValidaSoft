---
id: "tercero/cuentas-bancarias"
titulo: "Designa la cuenta en la que te paga una empresa"
actor: "Tercero"
dominio: "tercero"
resumen: "Tu validación con una empresa que exige cuenta bancaria espera hasta que designes una. Registras tu cuenta en «Mis cuentas» y eliges en cuál te paga cada empresa. Para designarla necesitas subir tu certificación bancaria, y la empresa la aprueba entre dos personas: en ese momento tu validación se cierra sola."
pasos: 6
---

# Designa la cuenta en la que te paga una empresa

**Actor:** Tercero · **Dominio:** Portal del tercero

Tu validación con una empresa que exige cuenta bancaria espera hasta que designes una. Registras tu cuenta en «Mis cuentas» y eliges en cuál te paga cada empresa. Para designarla necesitas subir tu certificación bancaria, y la empresa la aprueba entre dos personas: en ese momento tu validación se cierra sola.

**Antes de empezar**

- Un grupo empresarial con «Constructora Norte» (RUT y Cámara de comercio), con Compras y Cumplimiento, y el tipo de vínculo Proveedor con «Exige cuenta bancaria designada» activado.
- Un tercero que completó su invitación de Norte, no tiene ninguna cuenta registrada y cuyo expediente ya aprobaron las dos áreas.

**Pasos**

1. Mira por qué tu validación espera
2. Registra tu cuenta bancaria
3. Sube la certificación bancaria para designarla
4. Designa la cuenta y espera la aprobación
5. Confirma que la cuenta quedó designada
6. Tu validación se cierra con la cuenta aprobada

## Paso 1. Mira por qué tu validación espera

Las áreas de la empresa ya aprobaron tu información, pero tu validación sigue «En revisión»: la empresa exige que tengas una cuenta bancaria designada y aprobada, y todavía no la hay. El aviso «Falta tu cuenta bancaria designada» lo explica y «Ver mis cuentas» te lleva a registrarla. Cuando la empresa aprueba la cuenta, tu validación se cierra sola, sin que tengas que hacer nada más.

> **Pantalla:** `/expediente/{id}`

![Paso 1: Mira por qué tu validación espera (escritorio)](../../../salida/manual/img/tercero/cuentas-bancarias/01-mira-por-que-tu-validacion-espera-claro.webp)

![Paso 1: Mira por qué tu validación espera (celular)](../../../salida/manual/img/tercero/cuentas-bancarias/01-mira-por-que-tu-validacion-espera-celular.webp)

**Variantes**

- **La empresa no exige cuenta bancaria designada:** No ves este aviso: tu validación se cierra apenas las áreas aprueban.
- **Ya designaste una cuenta y la empresa no la ha aprobado:** El aviso sigue ahí, y te dice que esperes a que la empresa apruebe la que ya designaste.

**Configuración que lo afecta**

- **Exige cuenta bancaria designada (tipo de vínculo)** (`Configuración › Tipos de vínculo (/tipos-vinculo)`): Cada empresa decide si su tipo de vínculo exige que el tercero tenga una cuenta designada aprobada antes de cerrar la validación.

## Paso 2. Registra tu cuenta bancaria

En «Agregar cuenta bancaria» eliges el banco de la lista, el tipo de cuenta y escribes el número, el titular y su identificación. Al guardar, la cuenta queda registrada y solo ves sus últimos cuatro dígitos. Puedes tener varias cuentas y usar una u otra con cada empresa.

> **Pantalla:** `/cuentas`

![Paso 2: Registra tu cuenta bancaria (escritorio)](../../../salida/manual/img/tercero/cuentas-bancarias/02-registra-tu-cuenta-bancaria-claro.webp)

![Paso 2: Registra tu cuenta bancaria (celular)](../../../salida/manual/img/tercero/cuentas-bancarias/02-registra-tu-cuenta-bancaria-celular.webp)

## Paso 3. Sube la certificación bancaria para designarla

Para designar una cuenta la empresa necesita la certificación que expide tu banco, a tu nombre. Si no la tienes en tu bóveda, la pantalla te lleva a subirla desde aquí. Con el documento aprobado, la cuenta puede designarse para cada empresa que la pida.

> **Pantalla:** `/cuentas`

![Paso 3: Sube la certificación bancaria para designarla (escritorio)](../../../salida/manual/img/tercero/cuentas-bancarias/03-sube-la-certificacion-bancaria-para-designarla-claro.webp)

![Paso 3: Sube la certificación bancaria para designarla (celular)](../../../salida/manual/img/tercero/cuentas-bancarias/03-sube-la-certificacion-bancaria-para-designarla-celular.webp)

## Paso 4. Designa la cuenta y espera la aprobación

Al designar la cuenta para una empresa, queda «Pendiente de aprobación» hasta que la empresa la apruebe. La empresa revisa el número completo contra la certificación y la aprueban dos personas distintas antes de que la designación quede en firme.

> **Pantalla:** `/cuentas`

![Paso 4: Designa la cuenta y espera la aprobación (escritorio)](../../../salida/manual/img/tercero/cuentas-bancarias/04-designa-la-cuenta-y-espera-la-aprobacion-claro.webp)

![Paso 4: Designa la cuenta y espera la aprobación (celular)](../../../salida/manual/img/tercero/cuentas-bancarias/04-designa-la-cuenta-y-espera-la-aprobacion-celular.webp)

## Paso 5. Confirma que la cuenta quedó designada

Cuando las dos personas de la empresa aprueban, la cuenta aparece como «Designada para» esa empresa, y ahí te pagarán. Si cambias de cuenta más adelante, la nueva pasa también por aprobación.

> **Pantalla:** `/cuentas`

![Paso 5: Confirma que la cuenta quedó designada (escritorio)](../../../salida/manual/img/tercero/cuentas-bancarias/05-confirma-que-la-cuenta-quedo-designada-claro.webp)

![Paso 5: Confirma que la cuenta quedó designada (celular)](../../../salida/manual/img/tercero/cuentas-bancarias/05-confirma-que-la-cuenta-quedo-designada-celular.webp)

## Paso 6. Tu validación se cierra con la cuenta aprobada

En cuanto la segunda persona de la empresa aprobó tu cuenta, tu validación se aprobó sola: el aviso de la cuenta desapareció y tu vínculo con la empresa está activo. No tuviste que avisar ni reenviar nada. Si la empresa hubiera rechazado la cuenta, tu validación habría seguido esperando y verías el motivo para designar otra.

> **Pantalla:** `/expediente/{id}`

![Paso 6: Tu validación se cierra con la cuenta aprobada (escritorio)](../../../salida/manual/img/tercero/cuentas-bancarias/06-tu-validacion-se-cierra-con-la-cuenta-aprobada-claro.webp)

![Paso 6: Tu validación se cierra con la cuenta aprobada (celular)](../../../salida/manual/img/tercero/cuentas-bancarias/06-tu-validacion-se-cierra-con-la-cuenta-aprobada-celular.webp)

## Recorridos relacionados

- [Indica con quién hablar en cada empresa](contactos-por-empresa.md)
- [Entérate de los avisos desde la campana](campana-y-avisos.md)
- [Aprueba la cuenta bancaria designada de un proveedor](../vinculos/cuenta-designada-aprobar.md)
- [Crea un tipo de vínculo y lo desactiva](../configuracion/tipos-de-vinculo.md)
