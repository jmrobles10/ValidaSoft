---
id: "plataforma/plan-de-ia-y-llave-openai"
titulo: "Configura el plan de IA de una empresa y su llave de OpenAI"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma define el plan de IA de una empresa que aún no tiene uno: cuántas validaciones van incluidas, si se permiten adicionales y con qué tope, y la llave de OpenAI con la que se paga el análisis. La llave se guarda cifrada y no vuelve a verse en claro."
pasos: 5
---

# Configura el plan de IA de una empresa y su llave de OpenAI

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma define el plan de IA de una empresa que aún no tiene uno: cuántas validaciones van incluidas, si se permiten adicionales y con qué tope, y la llave de OpenAI con la que se paga el análisis. La llave se guarda cifrada y no vuelve a verse en claro.

**Antes de empezar**

- Un grupo con una empresa sin plan de IA.
- Un operador de Plataforma con la consola abierta.

**Pasos**

1. Abre el plan de IA de la empresa
2. Define el cupo mensual
3. Pega la llave de OpenAI y pruébala
4. Guarda el plan
5. Prueba la llave guardada

## Paso 1. Abre el plan de IA de la empresa

En la tabla del grupo, cada empresa tiene su «Plan de IA». Una empresa sin plan revisa sus documentos a mano; con plan, los analiza con IA. El plan es de la empresa, no del grupo: las demás empresas del mismo grupo conservan el suyo.

> **Pantalla:** `/plataforma/grupos`

![Paso 1: Abre el plan de IA de la empresa (escritorio)](../../../salida/manual/img/plataforma/plan-de-ia-y-llave-openai/01-abre-el-plan-de-ia-de-la-empresa-claro.webp)

**Configuración que lo afecta**

- **Plan de IA por empresa** (`Plataforma › Grupos y empresas › Plan de IA`): Define si la empresa usa IA para revisar documentos, cuánto va incluido y con qué llave se paga.

## Paso 2. Define el cupo mensual

Las validaciones incluidas son los análisis de IA que van en el plan cada mes. Al agotarlas, la empresa puede seguir validando con IA solo si permites adicionales; esos se cobran aparte. El tope limita cuántos adicionales puede consumir en el mes. Vacío significa sin tope.

> **Pantalla:** `/plataforma/grupos/{id}/empresas/{id}/plan-ia`

![Paso 2: Define el cupo mensual (escritorio)](../../../salida/manual/img/plataforma/plan-de-ia-y-llave-openai/02-define-el-cupo-mensual-claro.webp)

**Variantes**

- **No permites adicionales:** El resumen del plan dice «No se permiten» y no hay validaciones con IA fuera de las incluidas.
- **Permites adicionales sin tope:** El campo de tope se deja vacío y el resumen dice «Permitidos, sin tope».

**Configuración que lo afecta**

- **Validaciones incluidas y adicionales** (`Plataforma › Grupos y empresas › Plan de IA`): Fija el cupo mensual de análisis con IA de la empresa y lo que se cobra fuera del plan.

## Paso 3. Pega la llave de OpenAI y pruébala

La llave es la de la organización de OpenAI de esta empresa: con ella se paga el análisis. El campo la oculta mientras escribes y, al guardar, se cifra: no se vuelve a mostrar, ni a ti ni a nadie. «Probar la nueva llave» consulta a OpenAI sin consumir tokens, solo cuando lo pides, y te dice si la acepta o si no tiene cuota.

> **Pantalla:** `/plataforma/grupos/{id}/empresas/{id}/plan-ia`

![Paso 3: Pega la llave de OpenAI y pruébala (escritorio)](../../../salida/manual/img/plataforma/plan-de-ia-y-llave-openai/03-pega-la-llave-de-openai-y-pruebala-claro.webp)

**Variantes**

- **OpenAI rechaza la llave:** La pantalla dice «OpenAI rechazó la llave» con sus últimos cuatro caracteres.
- **La llave no tiene cuota:** La pantalla dice que la llave no tiene cuota o superó su límite.

**Configuración que lo afecta**

- **API key de OpenAI**: Es la llave con la que la empresa paga sus análisis; si no hay llave, no se analiza con IA.

## Paso 4. Guarda el plan

Al guardar, el plan queda activo y la llave aparece como «Configurada» con sus últimos cuatro caracteres. El campo para escribir una llave nueva desaparece: la llave guardada no se muestra completa en ningún lugar de la consola. Para reemplazarla, usa «Reemplazar»; para quitarla, «Quitar», y la empresa deja de pagar análisis con IA.

> **Pantalla:** `/plataforma/grupos/{id}/empresas/{id}/plan-ia`

![Paso 4: Guarda el plan (escritorio)](../../../salida/manual/img/plataforma/plan-de-ia-y-llave-openai/04-guarda-el-plan-claro.webp)

**Variantes**

- **El plan está inactivo:** La empresa no paga análisis con su llave y sus documentos se revisan manualmente.

**Configuración que lo afecta**

- **Plan activo**: Inactivo, la empresa no paga análisis con su llave aunque la tenga guardada.

## Paso 5. Prueba la llave guardada

Puedes probar la llave que ya está guardada sin volver a escribirla. Validasoft la prueba contra OpenAI y muestra el resultado con sus últimos cuatro caracteres. La prueba no consume tokens y solo se hace cuando la pides.

> **Pantalla:** `/plataforma/grupos/{id}/empresas/{id}/plan-ia`

![Paso 5: Prueba la llave guardada (escritorio)](../../../salida/manual/img/plataforma/plan-de-ia-y-llave-openai/05-prueba-la-llave-guardada-claro.webp)

## Recorridos relacionados

- [Crea un grupo con su primera empresa](crear-grupo-y-empresa.md)
- [Revisa el uso de IA de los clientes y cambia la tarifa del adicional](consumo-y-tarifa-de-ia.md)
