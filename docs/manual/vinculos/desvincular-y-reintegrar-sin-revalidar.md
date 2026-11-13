---
id: "vinculos/desvincular-y-reintegrar-sin-revalidar"
titulo: "Desvincula un tercero y lo reintegra sin revalidar"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora desvincula a un aliado que ya estaba aprobado y, mientras lo aprobado siga vigente, lo reintegra sin pedirle documentos nuevos. El certificado se revoca al desvincular y se emite de nuevo al reintegrar."
pasos: 5
---

# Desvincula un tercero y lo reintegra sin revalidar

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora desvincula a un aliado que ya estaba aprobado y, mientras lo aprobado siga vigente, lo reintegra sin pedirle documentos nuevos. El certificado se revoca al desvincular y se emite de nuevo al reintegrar.

**Antes de empezar**

- Un grupo con Constructora Norte, con el área Compras y una administradora.
- Un tipo de vínculo Aliado con un flujo de una etapa (Compras) y un vínculo ya aprobado y activo, con su certificado vigente.

**Pasos**

1. Abre las acciones del vínculo activo
2. Escribe el motivo de la desvinculación
3. Comprueba que el vínculo quedó desvinculado
4. Reintegra sin revalidar
5. Comprueba que el vínculo volvió a estar activo

## Paso 1. Abre las acciones del vínculo activo

Un vínculo activo tiene la acción «Desvincular» en «Más acciones». Desvincular corta la relación con el tercero: el vínculo deja de estar activo, su expediente abierto se anula y el certificado vigente se revoca. Lo aprobado no se borra y puedes reintegrarlo después.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Abre las acciones del vínculo activo (escritorio)](../../../salida/manual/img/vinculos/desvincular-y-reintegrar-sin-revalidar/01-abre-las-acciones-del-vinculo-activo-claro.webp)

**Configuración que lo afecta**

- **Usuarios y roles** (`Configuración › Usuarios y roles (/usuarios)`): Quién puede desvincular y reintegrar lo decide el permiso de cada rol.

## Paso 2. Escribe el motivo de la desvinculación

El motivo queda en el historial del vínculo y en la auditoría de la empresa. El tercero no ve el motivo.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Escribe el motivo de la desvinculación (escritorio)](../../../salida/manual/img/vinculos/desvincular-y-reintegrar-sin-revalidar/02-escribe-el-motivo-de-la-desvinculacion-claro.webp)

## Paso 3. Comprueba que el vínculo quedó desvinculado

El estado pasa a «Desvinculado» y el certificado del tercero queda revocado: sus sistemas integrados lo ven como no vigente. El historial del vínculo registra la desvinculación con su fecha.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Comprueba que el vínculo quedó desvinculado (escritorio)](../../../salida/manual/img/vinculos/desvincular-y-reintegrar-sin-revalidar/03-comprueba-que-el-vinculo-quedo-desvinculado-claro.webp)

## Paso 4. Reintegra sin revalidar

La opción «Reintegrar sin revalidar» restituye el estado que tenía el vínculo antes de desvincularlo y vuelve a emitir el certificado con lo que ya se aprobó. Solo está disponible si el vínculo fue aprobado y su aprobación sigue vigente. Si no, el sistema pide revalidar (ver el recorrido de reintegrar revalidando).

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 4: Reintegra sin revalidar (escritorio)](../../../salida/manual/img/vinculos/desvincular-y-reintegrar-sin-revalidar/04-reintegra-sin-revalidar-claro.webp)

**Variantes**

- **Algún documento del flujo venció:** La opción sin revalidar no se puede elegir y el diálogo explica por qué: hay que renovar los documentos.

## Paso 5. Comprueba que el vínculo volvió a estar activo

El vínculo vuelve a «Activo» sin abrir un expediente nuevo y se emite un certificado nuevo con lo aprobado. El tercero recibe un aviso por correo que dice que quedó como estaba, sin documentos nuevos.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 5: Comprueba que el vínculo volvió a estar activo (escritorio)](../../../salida/manual/img/vinculos/desvincular-y-reintegrar-sin-revalidar/05-comprueba-que-el-vinculo-volvio-a-estar-activo-claro.webp)

## Recorridos relacionados

- [Reintegra un vínculo revalidando](reintegrar-revalidando.md)
- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
