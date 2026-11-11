import { Alert, Chip, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import type { CruceDeRequisitoDto, CruceDeSeccionDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { SeccionFicha } from '../../terceros/SeccionesVinculo';
import { esResultadoCruce, porcentaje, tonoDeCampo } from '../cruce';

/**
 * Cruce de la sección con los documentos que la respaldan (F5, FM-8): por documento, la tabla campo por campo con lo
 * declarado, lo que dice el documento y el resultado, y el aviso de discrepancias críticas (FM-13). Sin análisis de IA para
 * la empresa, revisión manual. Es evidencia: no decide nada.
 */
export function CruceSeccion({ cruce }: { cruce: CruceDeRequisitoDto }) {
  const { t } = useTranslation();
  return (
    <SeccionFicha titulo={t('cruce.titulo')}>
      <Stack spacing={1.5}>
        {cruce.discrepanciaCritica && <Alert severity="error">{t('cruce.discrepanciaCritica')}</Alert>}
        {cruce.estado === 'SinDocumento' && (
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('cruce.sinDocumento')}
          </Typography>
        )}
        {cruce.sinCruce.length > 0 && (
          <Alert severity="info">
            {t('cruce.manual', { documentos: cruce.sinCruce.map((d) => t('fichaEmpresa.requisito.soporte', { tipo: d.nombreDocumento, n: String(d.version) })).join(', ') })}
          </Alert>
        )}
        {cruce.cruces.map((c) => (
          <TablaCruce key={`${c.documentoId}-${String(c.versionDocumento)}`} cruce={c} />
        ))}
        {cruce.cruces.length > 0 && (
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {t('cruce.evidencia')}
          </Typography>
        )}
      </Stack>
    </SeccionFicha>
  );
}

/** La tabla del cruce con una versión de documento. */
export function TablaCruce({ cruce }: { cruce: CruceDeSeccionDto }) {
  const { t } = useTranslation();
  const titulo = t('cruce.tituloCon', { documento: cruce.nombreDocumento, n: String(cruce.versionDocumento) });
  const vacio = t('cruce.vacio');
  return (
    <Stack spacing={0.75}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Typography variant="subtitle2" component="p">
          {titulo}
        </Typography>
        <Chip size="small" variant="outlined" label={t('cruce.confianza', { valor: porcentaje(Number(cruce.confianza)) })} />
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('cruce.resumen', { coinciden: String(cruce.coincidencias), difieren: String(cruce.discrepancias), noVerificables: String(cruce.noVerificables) })}
        </Typography>
      </Stack>
      {!cruce.legible && <Alert severity="warning">{t('cruce.ilegible')}</Alert>}
      <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, overflowX: 'auto' }}>
        <Table size="small" aria-label={titulo}>
          <TableHead>
            <TableRow>
              <TableCell>{t('cruce.campo')}</TableCell>
              <TableCell>{t('cruce.declarado')}</TableCell>
              <TableCell>{t('cruce.documento')}</TableCell>
              <TableCell>{t('cruce.resultado')}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {cruce.campos.map((campo, i) => {
              const color = tonoDeCampo(campo);
              const resultado = esResultadoCruce(campo.resultado) ? t(`cruce.resultados.${campo.resultado}`) : campo.resultado;
              return (
                <TableRow key={`${campo.campo}-${String(i)}`} data-resultado={campo.resultado} aria-label={`${campo.etiqueta}: ${resultado}`}>
                  <TableCell sx={{ borderLeft: 3, borderLeftColor: `estado.${color}.borde`, fontWeight: 500 }}>
                    {campo.etiqueta}
                    {campo.critico && <Chip size="small" variant="outlined" label={t('cruce.critico')} sx={{ ml: 1 }} />}
                  </TableCell>
                  <TableCell>{campo.declarado ?? vacio}</TableCell>
                  <TableCell>{campo.extraido ?? vacio}</TableCell>
                  <TableCell sx={{ color: `estado.${color}.texto` }}>
                    {resultado}
                    {campo.detalle && (
                      <Typography variant="caption" component="span" sx={{ display: 'block', color: 'text.secondary' }}>
                        {campo.detalle}
                      </Typography>
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
    </Stack>
  );
}
