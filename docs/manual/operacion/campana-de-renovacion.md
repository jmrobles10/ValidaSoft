---
id: "operacion/campana-de-renovacion"
titulo: "Abre la renovación de varios clientes a la vez"
actor: "Administrador de empresa"
dominio: "operacion"
resumen: "La administradora crea una campaña de renovación para los vínculos que vencen pronto: filtra, revisa a quién afecta sin enviar nada, confirma y la campaña abre la renovación de cada tercero en segundo plano."
pasos: 5
---

# Abre la renovación de varios clientes a la vez

**Actor:** Administrador de empresa · **Dominio:** Operación masiva y continua

La administradora crea una campaña de renovación para los vínculos que vencen pronto: filtra, revisa a quién afecta sin enviar nada, confirma y la campaña abre la renovación de cada tercero en segundo plano.

**Antes de empezar**

- Un grupo con Constructora Sur, su flujo de Cliente que pide el RUT y la ficha general, con vigencia de 90 días, y un cliente ya aprobado cuyo vínculo vence en unos meses.

**Pasos**

1. Abre las campañas de renovación
2. Filtra los vínculos y revisa a quién afecta
3. Nombra la campaña y pide confirmación
4. Confirma lo que va a pasar
5. Sigue el avance de la campaña

## Paso 1. Abre las campañas de renovación

Desde Vencimientos, «Campañas de renovación» lista las campañas que la empresa ha creado para renovar la validación de sus terceros en lote. Una campaña abre la renovación de muchos terceros a la vez, en lugar de hacerlo uno por uno.

> **Pantalla:** `/e/{empresa}/vencimientos/campanias`

![Paso 1: Abre las campañas de renovación (escritorio)](../../../salida/manual/img/operacion/campana-de-renovacion/01-abre-las-campanas-de-renovacion-claro.webp)

**Configuración que lo afecta**

- **Campañas de renovación** (`Vencimientos › Campañas de renovación`): Solo afecta a los vínculos que cumplen el filtro que elijas al crear la campaña.

## Paso 2. Filtra los vínculos y revisa a quién afecta

Eliges a quiénes renovar con «Vencen en (días)», el tipo de vínculo y el estado. Al pulsar «Ver a quiénes afecta» se calcula la vista previa sin enviar nada: los totales dicen cuántos se abren, cuántos se recuerdan, cuántos se omiten y a cuántos llega correo. La tabla muestra qué pasará con cada tercero. Incluye también lo que ya venció.

> **Pantalla:** `/e/{empresa}/vencimientos/campanias/nueva`

![Paso 2: Filtra los vínculos y revisa a quién afecta (escritorio)](../../../salida/manual/img/operacion/campana-de-renovacion/02-filtra-los-vinculos-y-revisa-a-quien-afecta-claro.webp)

**Variantes**

- **Ningún vínculo cumple el filtro:** Aparece «Ningún vínculo con este filtro necesita que se abra o se recuerde su renovación» y no se puede abrir la campaña.
- **El vínculo tiene la renovación ya abierta:** En vez de abrirse, se recuerda la renovación que ya tiene abierta.

## Paso 3. Nombra la campaña y pide confirmación

Escribe un nombre para reconocer la campaña luego; si lo dejas vacío se usa «Renovación del» y la fecha. «Abrir renovaciones» no envía nada todavía: primero muestra un aviso con lo que va a pasar.

> **Pantalla:** `/e/{empresa}/vencimientos/campanias/nueva`

![Paso 3: Nombra la campaña y pide confirmación (escritorio)](../../../salida/manual/img/operacion/campana-de-renovacion/03-nombra-la-campana-y-pide-confirmacion-claro.webp)

## Paso 4. Confirma lo que va a pasar

El aviso dice cuántos terceros recibirán la renovación, a cuántos llega un correo y que todos reciben un aviso en su campana. Se procesa en segundo plano, por tandas: puedes salir de la pantalla y seguir el avance en la campaña. Puedes pausarla o cancelarla cuando quieras; lo que ya se abrió sigue su curso.

> **Pantalla:** `/e/{empresa}/vencimientos/campanias/nueva`

![Paso 4: Confirma lo que va a pasar (escritorio)](../../../salida/manual/img/operacion/campana-de-renovacion/04-confirma-lo-que-va-a-pasar-claro.webp)

## Paso 5. Sigue el avance de la campaña

El detalle muestra la campaña con su estado (Procesando, Pausada, Completada o Con errores), el avance y una fila por cada vínculo con su acción: «Renovación abierta» o «Renovación recordada». Desde aquí puedes pausar, reanudar o cancelar la campaña mientras se procesa, y descargar el informe.

> **Pantalla:** `/e/{empresa}/vencimientos/campanias/{id}`

![Paso 5: Sigue el avance de la campaña (escritorio)](../../../salida/manual/img/operacion/campana-de-renovacion/05-sigue-el-avance-de-la-campana-claro.webp)

## Recorridos relacionados

- [Activa el recordatorio automático de observaciones](recordatorio-automatico-por-flujo.md)
