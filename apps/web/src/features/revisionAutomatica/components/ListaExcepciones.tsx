import { Box, Stack } from '@mui/material';
import type { ExcepcionDeRevisionDto } from '@validasoft/api-client';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ListaVirtual } from '../../../shared/ListaVirtual';
import { claveDeFila, debeVirtualizar } from '../modelo';

/** Alto aproximado de una fila (px) mientras no se mide: la lista virtual la mide sola al dibujarla. */
const ALTO_ESTIMADO = 132;
const SEPARACION = 12;

/** No hay otra página que pedir: la API devuelve todo (hasta 200) de una vez. */
const NO_HACER_NADA = () => undefined;

/**
 * Las filas de la bandeja: hasta 50 se dibujan enteras; con más, solo las visibles (más unas de margen) para que una
 * bandeja de cientos de excepciones siga fluida.
 */
export function ListaExcepciones({ filas, etiqueta, fila }: { filas: readonly ExcepcionDeRevisionDto[]; etiqueta: string; fila: (excepcion: ExcepcionDeRevisionDto) => ReactNode }) {
  const { t } = useTranslation();
  if (debeVirtualizar(filas.length)) {
    return (
      <ListaVirtual
        filas={filas}
        claveFila={claveDeFila}
        altoEstimado={ALTO_ESTIMADO}
        separacion={SEPARACION}
        etiqueta={etiqueta}
        renderFila={fila}
        hayMas={false}
        cargandoMas={false}
        alCargarMas={NO_HACER_NADA}
        textoCargandoMas={t('revisionAutomatica.excepciones.cargandoMas')}
        textoErrorMas={t('revisionAutomatica.excepciones.error')}
        textoReintentar={t('comun.reintentar')}
      />
    );
  }
  return (
    <Stack component="ul" aria-label={etiqueta} spacing={1.5} sx={{ listStyle: 'none', m: 0, p: 0 }}>
      {filas.map((excepcion) => (
        <Box component="li" key={claveDeFila(excepcion)} sx={{ minWidth: 0 }}>
          {fila(excepcion)}
        </Box>
      ))}
    </Stack>
  );
}
