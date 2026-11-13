---
id: "plataforma/acceso-de-soporte-a-una-empresa"
titulo: "Concede a soporte de Validasoft un acceso temporal y lo revoca"
actor: "Administrador de empresa"
dominio: "plataforma"
resumen: "La administradora de una empresa concede al soporte de Validasoft un acceso temporal de lectura: por una empresa, por unas horas y con una razón. Mientras está vigente, el soporte puede leer la configuración, los flujos, los vínculos y los expedientes, pero nunca documentos. Puede revocarlo antes de que venza."
pasos: 4
---

# Concede a soporte de Validasoft un acceso temporal y lo revoca

**Actor:** Administrador de empresa · **Dominio:** Consola de Plataforma

La administradora de una empresa concede al soporte de Validasoft un acceso temporal de lectura: por una empresa, por unas horas y con una razón. Mientras está vigente, el soporte puede leer la configuración, los flujos, los vínculos y los expedientes, pero nunca documentos. Puede revocarlo antes de que venza.

**Antes de empezar**

- Una empresa del grupo con su administradora en Validasoft.
- Sin accesos de soporte vigentes.

**Pasos**

1. Abre Accesos de soporte
2. Concede el acceso
3. Revisa el acceso vigente
4. Revoca el acceso antes de que venza

## Paso 1. Abre Accesos de soporte

El soporte de Validasoft no ve nada de tu grupo salvo que se lo concedas. Aquí quedan registrados los accesos temporales que concediste, con su estado y cuándo expiran. Mientras no concedas ninguno, la lista está vacía.

> **Pantalla:** `/e/{empresa}/accesos-soporte`

![Paso 1: Abre Accesos de soporte (escritorio)](../../../salida/manual/img/plataforma/acceso-de-soporte-a-una-empresa/01-abre-accesos-de-soporte-claro.webp)

**Configuración que lo afecta**

- **Accesos de soporte** (`Configuración › Accesos de soporte (/accesos-soporte)`): Un acceso de soporte es temporal, por empresa y con una razón; se puede revocar antes de que venza.

## Paso 2. Concede el acceso

Eliges la empresa, la duración en horas (de 1 a 72) y para qué lo necesitan. La razón es obligatoria y queda en la auditoría. Durante ese tiempo el soporte podrá leer la configuración, los flujos, los vínculos y los expedientes de la empresa. Nunca verá ni descargará documentos, ni podrá cambiar nada.

> **Pantalla:** `/e/{empresa}/accesos-soporte`

![Paso 2: Concede el acceso (escritorio)](../../../salida/manual/img/plataforma/acceso-de-soporte-a-una-empresa/02-concede-el-acceso-claro.webp)

**Variantes**

- **No se escribe para qué:** El diálogo no deja conceder el acceso sin una razón.

**Configuración que lo afecta**

- **Duración del acceso de soporte**: Pasado ese tiempo, el acceso expira solo; no hace falta revocarlo.

## Paso 3. Revisa el acceso vigente

El acceso aparece en la lista con su empresa, la razón, cuándo expira y su estado. Mientras esté vigente indica las horas y minutos que le quedan. Cuando expira, pasa a «Expirado» y el soporte deja de tener acceso sin que tengas que hacer nada.

> **Pantalla:** `/e/{empresa}/accesos-soporte`

![Paso 3: Revisa el acceso vigente (escritorio)](../../../salida/manual/img/plataforma/acceso-de-soporte-a-una-empresa/03-revisa-el-acceso-vigente-claro.webp)

**Configuración que lo afecta**

- **Estado del acceso de soporte**: Vigente, revocado o expirado; los revocados y expirados no se pueden reactivar, hay que conceder uno nuevo.

## Paso 4. Revoca el acceso antes de que venza

Si el soporte ya no lo necesita, revócalo: deja de entrar de inmediato. Si vuelve a hacer falta, hay que conceder un acceso nuevo; el revocado no se reactiva.

> **Pantalla:** `/e/{empresa}/accesos-soporte`

![Paso 4: Revoca el acceso antes de que venza (escritorio)](../../../salida/manual/img/plataforma/acceso-de-soporte-a-una-empresa/04-revoca-el-acceso-antes-de-que-venza-claro.webp)

**Configuración que lo afecta**

- **Revocar un acceso de soporte**: Corta el acceso de soporte de inmediato.

## Recorridos relacionados

- [Revisa quién entró a la consola y filtra los ingresos fallidos](ingresos-de-los-operadores.md)
- [Crea un operador de la consola y le quita el acceso](operadores.md)
