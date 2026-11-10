import { Alert, AlertTitle, Box } from '@mui/material';
import type { FaltanteSeccionDto } from '@validasoft/api-client';
import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * Los faltantes exactos que devolvió el servidor al guardar (o los de la versión guardada), con su motivo legible. Cada
 * vez que cambia `senal` (un guardado incompleto) recibe el foco para que el tercero vea qué le falta.
 */
export function Faltantes({ faltantes, senal }: { faltantes: readonly FaltanteSeccionDto[]; senal: number }) {
  const { t } = useTranslation();
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (senal > 0) ref.current?.focus();
  }, [senal]);

  if (faltantes.length === 0) return null;
  const titulo = t('ficha.faltantesTitulo', { count: faltantes.length });
  return (
    <Alert ref={ref} tabIndex={-1} severity="warning" aria-label={titulo} sx={{ outline: 'none' }}>
      <AlertTitle>{titulo}</AlertTitle>
      <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
        {faltantes.map((f, i) => (
          <li key={`${f.campo ?? f.regla ?? ''}-${String(i)}`}>{f.mensaje}</li>
        ))}
      </Box>
      <Box sx={{ mt: 0.5, color: 'text.secondary', fontSize: 13 }}>{t('ficha.faltantesAyuda')}</Box>
    </Alert>
  );
}
