---
id: "revision/etapas-en-secuencia"
titulo: "Sigue un expediente que pasa por tres etapas"
actor: "Revisor/área"
dominio: "revision"
resumen: "El flujo de un aliado tiene tres etapas en orden: Compras, Legal y Cumplimiento. Rubén aprueba la primera; el expediente pasa a la segunda y Olga lo ve en su bandeja solo cuando le toca."
pasos: 4
---

# Sigue un expediente que pasa por tres etapas

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

El flujo de un aliado tiene tres etapas en orden: Compras, Legal y Cumplimiento. Rubén aprueba la primera; el expediente pasa a la segunda y Olga lo ve en su bandeja solo cuando le toca.

**Antes de empezar**

- Un grupo con Constructora Norte, con las áreas Compras, Legal (miembro: Olga) y Cumplimiento (también Olga; puede rechazar).
- Un flujo de tipo Aliado con tres etapas en secuencia: Compras, luego Legal, luego Cumplimiento. Sin documentos que subir.

**Pasos**

1. Mira las etapas del expediente
2. Emite el veredicto de tu etapa
3. Ve cómo avanza el expediente a la siguiente etapa
4. Revisa la bandeja de Olga con la etapa 2 en curso

## Paso 1. Mira las etapas del expediente

En «Etapas y áreas» cada etapa aparece con su estado. La etapa en curso es la que decide ahora; las siguientes están «En espera» hasta que la anterior se apruebe por completo. Las áreas de una misma etapa revisan al mismo tiempo, pero una etapa no empieza mientras la anterior no termina.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Mira las etapas del expediente (escritorio)](../../../salida/manual/img/revision/etapas-en-secuencia/01-mira-las-etapas-del-expediente-claro.webp)

**Variantes**

- **Todas las áreas están en la misma etapa:** Todas revisan al mismo tiempo, y el expediente se aprueba cuando todas aprueban.

## Paso 2. Emite el veredicto de tu etapa

Con «Emitir veredicto» se abre la decisión de tu área. Aprobar cierra tu parte de la etapa; si observas, el expediente vuelve al tercero. Al aprobar, la etapa pasa a «Aprobada» y la siguiente empieza sola.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Emite el veredicto de tu etapa (escritorio)](../../../salida/manual/img/revision/etapas-en-secuencia/02-emite-el-veredicto-de-tu-etapa-claro.webp)

## Paso 3. Ve cómo avanza el expediente a la siguiente etapa

La etapa 1 queda «Aprobada» y la etapa 2, Legal, pasa a «En curso». La etapa 3 sigue «En espera». El expediente solo aparece en la bandeja de las áreas de la etapa en curso: Legal lo verá ahora y Cumplimiento lo verá cuando Legal apruebe.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Ve cómo avanza el expediente a la siguiente etapa (escritorio)](../../../salida/manual/img/revision/etapas-en-secuencia/03-ve-como-avanza-el-expediente-a-la-siguiente-etap-claro.webp)

## Paso 4. Revisa la bandeja de Olga con la etapa 2 en curso

Olga es de Legal y de Cumplimiento, pero solo le llega el expediente de Legal, que es la etapa en curso. El de Cumplimiento aparecerá cuando Legal apruebe. Así cada área ve el expediente solo cuando le toca decidir.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 4: Revisa la bandeja de Olga con la etapa 2 en curso (escritorio)](../../../salida/manual/img/revision/etapas-en-secuencia/04-revisa-la-bandeja-de-olga-con-la-etapa-2-en-curs-claro.webp)

## Recorridos relacionados

- [Aprueba el área de Compras](aprobar-un-area.md)
- [Aprueba un área que pide dos personas distintas](dos-aprobadores-y-separacion-de-funciones.md)
