---
id: "configuracion/plan-del-grupo"
titulo: "Revisa el plan del grupo y su uso del mes"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora abre Plan del grupo para ver qué permite el plan que fijó Plataforma, cuánto lleva usado el grupo y qué pasa al llegar a un límite. El grupo está en el límite de empresas."
pasos: 2
---

# Revisa el plan del grupo y su uso del mes

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora abre Plan del grupo para ver qué permite el plan que fijó Plataforma, cuánto lleva usado el grupo y qué pasa al llegar a un límite. El grupo está en el límite de empresas.

**Antes de empezar**

- Un grupo empresarial con las empresas «Constructora Norte S.A.S.» y «Constructora Sur S.A.S.».
- Plataforma fijó al grupo el plan «empresarial» con límite de 2 empresas, 20 usuarios y bloqueo al llegar al límite.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Plan del grupo
2. Revisa el uso contra los límites

## Paso 1. Abre Configuración › Plan del grupo

La tarjeta «Tu plan» muestra el código del plan que te asignó Plataforma y qué pasa al llegar a un límite. Con el bloqueo activo no se pueden crear más usuarios, empresas ni vínculos de lo que ya se agotó; lo que ya existe sigue funcionando. Sin bloqueo, Validasoft solo avisa a los administradores del grupo y se puede seguir trabajando.

> **Pantalla:** `/e/{empresa}/plan-grupo`

![Paso 1: Abre Configuración › Plan del grupo (escritorio)](../../../salida/manual/img/configuracion/plan-del-grupo/01-abre-configuracion-plan-del-grupo-claro.webp)

**Variantes**

- **Plataforma no le ha fijado plan al grupo:** La tarjeta dice «Sin plan asignado»: no hay límites y solo se mide el uso.
- **El bloqueo al llegar al límite está apagado:** La tarjeta dice que solo se avisa y el grupo puede seguir creando elementos.

**Configuración que lo afecta**

- **Plan del grupo** (`Configuración › Plan del grupo (/plan-grupo)`): Los límites los fija Plataforma; esta pantalla solo muestra lo que permiten y cuánto se usa.

## Paso 2. Revisa el uso contra los límites

Cada medidor muestra lo usado y su límite, con una barra. Usuarios, empresas y vínculos activos cuentan lo que hay hoy; el almacenamiento suma lo que el grupo recibe de sus terceros, y las llamadas a la API se cuentan en el mes. En el recuadro 1 el grupo tiene 2 de 2 empresas, y el recuadro 2 avisa «Llegaste al límite de tu plan»: para crear otra empresa, Plataforma tiene que ampliar el plan.

> **Pantalla:** `/e/{empresa}/plan-grupo`

![Paso 2: Revisa el uso contra los límites (escritorio)](../../../salida/manual/img/configuracion/plan-del-grupo/02-revisa-el-uso-contra-los-limites-claro.webp)

**Variantes**

- **Un medidor llega al 80 %:** Aparece el aviso «Estás cerca del límite de tu plan» con el medidor que va llegando.
- **Un medidor no tiene límite:** Muestra solo lo usado con la frase «sin límite».

## Recorridos relacionados

- [Agrega una empresa del grupo y publica sus textos legales](empresas-y-textos-legales.md)
