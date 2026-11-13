---
id: "plataforma/consumo-y-tarifa-de-ia"
titulo: "Revisa el uso de IA de los clientes y cambia la tarifa del adicional"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma revisa cuánto consumió cada cliente en el mes en validaciones con IA: las incluidas en su plan, las adicionales, los tokens y el costo del proveedor. Luego cambia la tarifa general con la que se cobra cada validación adicional."
pasos: 4
---

# Revisa el uso de IA de los clientes y cambia la tarifa del adicional

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma revisa cuánto consumió cada cliente en el mes en validaciones con IA: las incluidas en su plan, las adicionales, los tokens y el costo del proveedor. Luego cambia la tarifa general con la que se cobra cada validación adicional.

**Antes de empezar**

- Un grupo con empresas, con o sin consumo de IA en el mes.
- Un operador de Plataforma con la consola abierta.

**Pasos**

1. Abre Uso de IA y elige el mes
2. Lee el consumo de cada cliente
3. Cambia la tarifa general del adicional
4. Comprueba la tarifa vigente

## Paso 1. Abre Uso de IA y elige el mes

La pantalla lista, para cada grupo y empresa, lo que consumió en el mes elegido: validaciones incluidas y adicionales, tokens, costo del proveedor y el valor de lo adicional. Arriba, el mes define el período de todas las cifras; el buscador filtra por grupo o empresa. Los totales del mes suman todos los clientes con consumo.

> **Pantalla:** `/plataforma/uso-ia`

![Paso 1: Abre Uso de IA y elige el mes (escritorio)](../../../salida/manual/img/plataforma/consumo-y-tarifa-de-ia/01-abre-uso-de-ia-y-elige-el-mes-claro.webp)

**Configuración que lo afecta**

- **Mes del uso de IA** (`Plataforma › Uso de IA`): Cambia el período de los totales y de la tabla; el archivo exportado sale con el mismo mes.

## Paso 2. Lee el consumo de cada cliente

Cada fila es un grupo, con sus empresas plegadas: «Incluidas usadas» cuenta las validaciones del plan, «Adicionales» las que se cobran aparte, y «Tokens» separa entrada y salida. El costo en dólares es el que reporta el proveedor de IA por los tokens usados. «Valor adicional» es lo consumido por encima del plan, a la tarifa de cada empresa. Si ningún cliente consumió en el mes, la pantalla lo dice.

> **Pantalla:** `/plataforma/uso-ia`

![Paso 2: Lee el consumo de cada cliente (escritorio)](../../../salida/manual/img/plataforma/consumo-y-tarifa-de-ia/02-lee-el-consumo-de-cada-cliente-claro.webp)

**Variantes**

- **La empresa tiene precio propio por adicional:** La fila lo muestra como «Precio propio» y su valor adicional se calcula con ese precio, no con la tarifa general.
- **La empresa no permite adicionales:** Aparece «Sin adicionales» y su valor adicional es cero.
- **No hay consumo en el mes:** La pantalla dice «Nadie consumió IA» en ese mes y explica que el consumo aparece cuando una empresa con plan valida documentos.

## Paso 3. Cambia la tarifa general del adicional

La tarifa general es lo que se cobra por cada validación que una empresa consume por encima de las incluidas en su plan. Escribes el precio (hasta dos decimales) y la moneda en código ISO, como COP o USD. Se aplica a las empresas que no tienen un precio propio; una empresa con precio propio en su plan de IA conserva el suyo.

> **Pantalla:** `/plataforma/uso-ia`

![Paso 3: Cambia la tarifa general del adicional (escritorio)](../../../salida/manual/img/plataforma/consumo-y-tarifa-de-ia/03-cambia-la-tarifa-general-del-adicional-claro.webp)

**Variantes**

- **El precio queda en cero:** Lo adicional no se valora: la pantalla avisa que aún no hay tarifa.
- **El precio tiene más de dos decimales o es negativo:** El diálogo indica que el precio va entre 0 y 100 000 000, con hasta dos decimales.

**Configuración que lo afecta**

- **Tarifa general del adicional** (`Plataforma › Uso de IA`): Es el precio por defecto de las validaciones adicionales de todas las empresas sin precio propio.

## Paso 4. Comprueba la tarifa vigente

Validasoft confirma que guardó la tarifa y la muestra con su fecha y quién la cambió. Desde el próximo cálculo, el valor de lo adicional de las empresas sin precio propio usa este precio. Las empresas con precio propio no cambian.

> **Pantalla:** `/plataforma/uso-ia`

![Paso 4: Comprueba la tarifa vigente (escritorio)](../../../salida/manual/img/plataforma/consumo-y-tarifa-de-ia/04-comprueba-la-tarifa-vigente-claro.webp)

## Recorridos relacionados

- [Configura el plan de IA de una empresa y su llave de OpenAI](plan-de-ia-y-llave-openai.md)
- [Fija el plan y los límites de un grupo](plan-del-grupo.md)
