import { Box, Button, Typography } from '@mui/material';
import type { DatoDeclaradoDto } from '@validasoft/api-client';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatearDatoDeclarado } from './formatoDatos';

export interface DatosDeclaradosProps {
  datos: readonly DatoDeclaradoDto[];
  /** Muestra solo los primeros y un botón para ver el resto (en las tarjetas; el visor los muestra todos). */
  limite?: number;
  /** Título visible de la lista; sin él, la lista se etiqueta para lectores de pantalla. */
  conTitulo?: boolean;
}

/**
 * Lo que el tercero declaró en la versión (TD-4): etiqueta y valor formateado según el tipo del campo (fecha legible,
 * Sí/No, número con separadores es-CO). Lista de definiciones en dos columnas.
 */
export function DatosDeclarados({ datos, limite, conTitulo = false }: DatosDeclaradosProps) {
  const { t } = useTranslation();
  const [todos, setTodos] = useState(false);
  if (datos.length === 0) return null;
  const visibles = limite && !todos ? datos.slice(0, limite) : datos;
  const ocultos = datos.length - visibles.length;
  const textos = { si: t('expedientes.datosDeclarados.si'), no: t('expedientes.datosDeclarados.no') };

  return (
    <Box>
      {conTitulo && (
        <Typography variant="overline" component="h3" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
          {t('expedientes.datosDeclarados.titulo')}
        </Typography>
      )}
      <Box
        component="dl"
        aria-label={conTitulo ? undefined : t('expedientes.datosDeclarados.titulo')}
        sx={{ m: 0, display: 'grid', gridTemplateColumns: 'minmax(0, max-content) minmax(0, 1fr)', columnGap: 1.5, rowGap: 0.5, fontSize: 13, lineHeight: '18px' }}
      >
        {visibles.map((dato) => (
          <Box key={dato.clave} sx={{ display: 'contents' }}>
            <Box component="dt" sx={{ color: 'text.secondary' }}>
              {dato.etiqueta}
            </Box>
            <Box component="dd" sx={{ m: 0, overflowWrap: 'anywhere', fontVariantNumeric: dato.tipo === 'Numero' ? 'tabular-nums' : undefined }}>
              {formatearDatoDeclarado(dato, textos)}
            </Box>
          </Box>
        ))}
      </Box>
      {limite !== undefined && datos.length > limite && (
        <Button size="small" variant="text" onClick={() => setTodos((v) => !v)} sx={{ mt: 0.5, px: 0.5, minWidth: 0 }} aria-expanded={todos}>
          {todos ? t('expedientes.datosDeclarados.verMenos') : t('expedientes.datosDeclarados.verTodos', { count: ocultos })}
        </Button>
      )}
    </Box>
  );
}
