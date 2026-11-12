import { Box, Divider, Stack, Typography } from '@mui/material';
import { Dato, RejillaDatos } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { DeclaracionPep } from './DeclaracionPep';
import type { DescargaDeclaracionPep } from './descargaDeclaracionPep';
import { declaracionActiva, esListaDeObjetos, esObjeto, etiquetaCampo, formatearValor, tituloDeFila } from './formato';

/**
 * Los datos de una sección en solo lectura, en el orden en que los guardó el esquema: valores simples en la rejilla;
 * grupos (dirección por partes, resoluciones, moneda extranjera) y filas (personas, ICA) debajo, con su título. La declaración jurada
 * PEP de una persona con «Sí» en PEP va aparte, como un bloque con su PDF (HU 49093): con `empresaId`, `terceroId` y `seccion` se
 * puede descargar (`version`: la que se está viendo; sin ella, la vigente).
 */
export function DatosDeSeccion({ datos, empresaId, terceroId, seccion, version = null }: Props) {
  const { t } = useTranslation();
  const descarga: DescargaDeclaracionPep | null = empresaId && terceroId && seccion ? { empresaId, terceroId, seccion, version } : null;
  const declaracionDelTercero = declaracionActiva(datos);
  const entradas = Object.entries(datos).filter(([clave, v]) => clave !== 'declaracion_pep' && v !== null && v !== undefined && !(Array.isArray(v) && v.length === 0));
  const simples = entradas.filter(([, v]) => !esObjeto(v) && !esListaDeObjetos(v));
  const grupos = entradas.filter(([, v]) => esObjeto(v));
  const filas = entradas.filter(([, v]) => esListaDeObjetos(v));

  if (entradas.length === 0 && !declaracionDelTercero) {
    return (
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {t('fichaMaestra.sinDatos')}
      </Typography>
    );
  }

  return (
    <Stack spacing={1.5}>
      {simples.length > 0 && <Rejilla datos={Object.fromEntries(simples)} />}
      {declaracionDelTercero && <DeclaracionPep declaracion={declaracionDelTercero} indice={null} descarga={descarga} />}
      {grupos.map(([clave, valor]) => (
        <Grupo key={clave} titulo={etiquetaCampo(clave, t)}>
          <Rejilla datos={esObjeto(valor) ? valor : {}} />
        </Grupo>
      ))}
      {filas.map(([clave, valor]) => (
        <Grupo key={clave} titulo={etiquetaCampo(clave, t)}>
          <Stack component="ol" divider={<Divider component="li" aria-hidden />} spacing={1} sx={{ p: 0, m: 0, listStyle: 'none' }}>
            {(esListaDeObjetos(valor) ? valor : []).map((fila, i) => {
              const titulo = tituloDeFila(clave, fila, i, t);
              const { declaracion_pep: _declaracion, ...resto } = fila;
              const declaracion = clave === 'personas' ? declaracionActiva(fila) : null;
              return (
                <Box component="li" key={`${clave}-${String(i)}`} aria-label={titulo}>
                  <Typography sx={{ fontWeight: 500, mb: 0.5 }}>{titulo}</Typography>
                  <Rejilla datos={resto} />
                  {declaracion && <DeclaracionPep declaracion={declaracion} indice={i} descarga={descarga} />}
                </Box>
              );
            })}
          </Stack>
        </Grupo>
      ))}
    </Stack>
  );
}

interface Props {
  datos: Record<string, unknown>;
  empresaId?: string;
  terceroId?: string;
  /** Código de la sección (`FICHA_LEGAL`): con él y el tercero se descarga el PDF de la declaración PEP. */
  seccion?: string;
  version?: number | null;
}

function Grupo({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <Stack component="section" aria-label={titulo} spacing={0.5}>
      <Typography variant="overline" component="h5" sx={{ color: 'text.secondary' }}>
        {titulo}
      </Typography>
      {children}
    </Stack>
  );
}

/** Rejilla de pares etiqueta/valor (un nivel; un grupo anidado se muestra en una línea). */
function Rejilla({ datos }: { datos: Record<string, unknown> }) {
  const { t } = useTranslation();
  return (
    <RejillaDatos columnas={2}>
      {Object.entries(datos)
        .filter(([, v]) => v !== null && v !== undefined)
        .map(([clave, valor]) => (
          <Dato key={clave} etiqueta={etiquetaCampo(clave, t)}>
            {esObjeto(valor)
              ? Object.entries(valor)
                  .map(([k, v]) => `${etiquetaCampo(k, t)}: ${formatearValor(k, v, t)}`)
                  .join(' · ')
              : formatearValor(clave, valor, t)}
          </Dato>
        ))}
    </RejillaDatos>
  );
}
