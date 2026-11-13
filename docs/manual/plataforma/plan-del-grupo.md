---
id: "plataforma/plan-del-grupo"
titulo: "Fija el plan y los límites de un grupo"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma define el plan de un grupo: su código, los límites de empresas, usuarios y demás recursos, y si al llegar a un límite el grupo se bloquea o solo recibe un aviso. Al guardar, la pantalla muestra el uso actual frente a cada límite."
pasos: 4
---

# Fija el plan y los límites de un grupo

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma define el plan de un grupo: su código, los límites de empresas, usuarios y demás recursos, y si al llegar a un límite el grupo se bloquea o solo recibe un aviso. Al guardar, la pantalla muestra el uso actual frente a cada límite.

**Antes de empezar**

- Un grupo sin plan, con dos empresas.
- Un operador de Plataforma con la consola abierta.

**Pasos**

1. Abre el plan del grupo
2. Define el código y los límites
3. Elige qué pasa al llegar a un límite
4. Guarda el plan y revisa el uso

## Paso 1. Abre el plan del grupo

En la tarjeta del grupo, el enlace «Plan y límites» abre la pantalla del plan. Un grupo sin plan no tiene límites: solo se mide su uso. Desde aquí se fija el plan del grupo entero; el plan de IA se configura aparte, empresa por empresa.

> **Pantalla:** `/plataforma/grupos`

![Paso 1: Abre el plan del grupo (escritorio)](../../../salida/manual/img/plataforma/plan-del-grupo/01-abre-el-plan-del-grupo-claro.webp)

**Configuración que lo afecta**

- **Plan del grupo** (`Plataforma › Grupos y empresas › Plan y límites`): Define los límites del grupo y si al llegar a uno se bloquea.

## Paso 2. Define el código y los límites

El código del plan es un nombre corto, como «estándar» o «empresarial», que el grupo ve en su pantalla de plan. Cada límite cuenta lo que ya existe: empresas activas, personas activas, terceros vinculados, documentos recibidos, almacenamiento y llamadas a la API por mes. Deja un campo vacío para no poner límite a ese recurso. Validasoft avisa al 80 % y al 100 % de cada uno.

> **Pantalla:** `/plataforma/grupos/{id}/plan`

![Paso 2: Define el código y los límites (escritorio)](../../../salida/manual/img/plataforma/plan-del-grupo/02-define-el-codigo-y-los-limites-claro.webp)

**Variantes**

- **Un campo queda vacío:** Ese recurso no tiene límite y se muestra como «Sin límite».
- **El límite de llamadas a la API se agota:** Las escrituras con API key se rechazan; las lecturas siguen funcionando.

## Paso 3. Elige qué pasa al llegar a un límite

El interruptor «Bloquear al llegar al límite» decide el comportamiento. Apagado, al llegar a un límite solo se avisa a los administradores del grupo y el grupo puede seguir creando. Encendido, el grupo no puede crear más de ese recurso hasta que amplíes el plan; lo que ya existe sigue funcionando.

> **Pantalla:** `/plataforma/grupos/{id}/plan`

![Paso 3: Elige qué pasa al llegar a un límite (escritorio)](../../../salida/manual/img/plataforma/plan-del-grupo/03-elige-que-pasa-al-llegar-a-un-limite-claro.webp)

**Variantes**

- **Interruptor apagado:** Se avisa, pero no se bloquea nada.
- **Interruptor encendido:** Al llegar al límite, las altas de ese recurso se rechazan con un mensaje que dice por qué.

**Configuración que lo afecta**

- **Bloquear al llegar al límite** (`Plataforma › Grupos y empresas › Plan y límites`): Decide si un límite alcanzado impide crear más o solo avisa.

## Paso 4. Guarda el plan y revisa el uso

Al guardar, el plan queda vigente para el grupo y la pantalla muestra su uso actual frente a cada límite. Aquí, el grupo ya tiene 2 de 2 empresas y aparece «En el límite»: con el bloqueo encendido, la siguiente empresa no se puede crear. Para ampliar el plan basta con subir el límite y guardar de nuevo.

> **Pantalla:** `/plataforma/grupos/{id}/plan`

![Paso 4: Guarda el plan y revisa el uso (escritorio)](../../../salida/manual/img/plataforma/plan-del-grupo/04-guarda-el-plan-y-revisa-el-uso-claro.webp)

**Variantes**

- **El uso supera el 80 % de un límite:** Se avisa a los administradores del grupo antes de llegar al tope.

**Configuración que lo afecta**

- **Uso de los límites**: Se mide cada 15 minutos; los avisos llegan a los administradores del grupo.

## Recorridos relacionados

- [Busca un grupo y revisa sus empresas](lista-de-grupos.md)
- [Revisa el uso de IA de los clientes y cambia la tarifa del adicional](consumo-y-tarifa-de-ia.md)
