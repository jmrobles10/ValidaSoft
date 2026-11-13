---
id: "operacion/delegacion-de-revision"
titulo: "Delega tu revisión mientras estás ausente"
actor: "Revisor/área"
dominio: "operacion"
resumen: "Rubén, revisor de Compras, delega su revisión en Olga por unas fechas de vacaciones. Olga ve en su bandeja los expedientes de Compras durante ese tiempo, y Rubén puede revocar la delegación cuando quiera."
pasos: 5
---

# Delega tu revisión mientras estás ausente

**Actor:** Revisor/área · **Dominio:** Operación masiva y continua

Rubén, revisor de Compras, delega su revisión en Olga por unas fechas de vacaciones. Olga ve en su bandeja los expedientes de Compras durante ese tiempo, y Rubén puede revocar la delegación cuando quiera.

**Antes de empezar**

- Un grupo con Constructora Norte, Rubén como miembro del área Compras y Olga como oficial, y un proveedor que ya entregó sus documentos y quedó en revisión.

**Pasos**

1. Abre la delegación de revisión
2. Elige quién revisa y las fechas
3. Comprueba la delegación vigente
4. Revisa el área delegada en tu bandeja
5. Revoca la delegación

## Paso 1. Abre la delegación de revisión

En «Delegación de revisión» ves quién revisa en tu lugar. Si no hay ninguna delegación vigente, la lista lo dice. «Delegar mi revisión» crea una delegación para que otra persona revise tu área entre dos fechas.

> **Pantalla:** `/e/{empresa}/delegaciones`

![Paso 1: Abre la delegación de revisión (escritorio)](../../../salida/manual/img/operacion/delegacion-de-revision/01-abre-la-delegacion-de-revision-claro.webp)

**Configuración que lo afecta**

- **Delegación de revisión** (`Configuración › Delegación de revisión (/delegaciones)`): Mientras la delegación esté vigente, la persona delegada ve y decide los expedientes de tu área.

## Paso 2. Elige quién revisa y las fechas

Eliges a la persona que revisa en tu lugar el rango de fechas (hasta 366 días) y, si quieres, las áreas que delegas: sin elegir ninguna, delegas todas las tuyas. El motivo es opcional y queda en el historial. Antes de crear la delegación, el diálogo dice quién revisará y recuerda que la separación de funciones se siguen aplicando a la persona delegada.

> **Pantalla:** `/e/{empresa}/delegaciones`

![Paso 2: Elige quién revisa y las fechas (escritorio)](../../../salida/manual/img/operacion/delegacion-de-revision/02-elige-quien-revisa-y-las-fechas-claro.webp)

## Paso 3. Comprueba la delegación vigente

La fila queda como «Vigente» con las fechas que elegiste. Desde ese momento Olga recibe en su bandeja los expedientes de Compras, y tú los sigues viendo como siempre.

> **Pantalla:** `/e/{empresa}/delegaciones`

![Paso 3: Comprueba la delegación vigente (escritorio)](../../../salida/manual/img/operacion/delegacion-de-revision/03-comprueba-la-delegacion-vigente-claro.webp)

## Paso 4. Revisa el área delegada en tu bandeja

Olga ve el expediente de Compras en su bandeja, aunque Compras no sea su área. Lo decide como revisora delegada,.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 4: Revisa el área delegada en tu bandeja (escritorio)](../../../salida/manual/img/operacion/delegacion-de-revision/04-revisa-el-area-delegada-en-tu-bandeja-claro.webp)

**Variantes**

- **La delegación ya venció o fue revocada:** El expediente deja de aparecer en la bandeja de la persona delegada.

## Paso 5. Revoca la delegación

Al revocarla, Olga deja de ver los expedientes de tu área. Úsala si cambian tus fechas de ausencia antes de que termine el rango.

> **Pantalla:** `/e/{empresa}/delegaciones`

![Paso 5: Revoca la delegación (escritorio)](../../../salida/manual/img/operacion/delegacion-de-revision/05-revoca-la-delegacion-claro.webp)

## Recorridos relacionados

- [Fija el plazo de revisión de cada área](plazos-de-revision.md)
