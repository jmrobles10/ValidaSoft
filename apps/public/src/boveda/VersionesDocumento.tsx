import { Alert, Box, Button, Stack, Typography } from '@mui/material';
import type { VersionDocumentoDto } from '@validasoft/api-client';
import { ChipEstado, EsqueletoTarjetas, formatearFecha } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { useDocumentoBoveda } from '../shared/api';
import { ErrorRecuperable } from '../shared/componentes';
import { formatearTamano } from './subida';
import { useDescargar } from './useDescargar';
import { mensajeDevolucion, sePuedeDescargar, sePuedeRetirar } from './versiones';

/** Todas las versiones del documento, de la más reciente a la más antigua; las devueltas con su motivo. */
export function VersionesDocumento({ documentoId, onRetirar }: { documentoId: string; onRetirar: (version: VersionDocumentoDto) => void }) {
  const { t } = useTranslation();
  const consulta = useDocumentoBoveda(documentoId, true);
  const descargar = useDescargar();

  if (consulta.isPending) return <EsqueletoTarjetas cantidad={2} lineas={1} sx={{ mt: 1.5 }} />;
  if (consulta.isError) return <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />;

  const versiones = [...consulta.data.versiones].sort((a, b) => b.n - a.n);
  return (
    <Stack component="ol" spacing={1} sx={{ p: 0, m: 0, mt: 1.5, listStyle: 'none' }}>
      {versiones.map((v) => (
        <Box component="li" key={v.n} sx={{ p: 1.5, borderRadius: 1, bgcolor: 'superficie.nivel2' }}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Typography sx={{ fontWeight: 600, fontSize: 14 }}>{t('comun.version', { n: String(v.n) })}</Typography>
            <ChipEstado estado={v.estado} fecha={v.cargadaEn} />
          </Stack>
          <Typography sx={{ color: 'text.secondary', fontSize: 13, mt: 0.5, wordBreak: 'break-word' }}>
            {v.nombreOriginal} · {formatearTamano(v.tamanoBytes)} · {formatearFecha(v.cargadaEn)}
          </Typography>
          {v.estado === 'Devuelta' && (
            <Alert severity="error" sx={{ mt: 1 }}>
              {mensajeDevolucion(v, t)}
            </Alert>
          )}
          {(sePuedeDescargar(v) || sePuedeRetirar(v)) && (
            <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
              {sePuedeDescargar(v) && (
                <Button size="small" variant="text" onClick={() => descargar(documentoId, v.n)}>
                  {t('boveda.descargar')}
                </Button>
              )}
              {sePuedeRetirar(v) && (
                <Button size="small" variant="text" color="error" onClick={() => onRetirar(v)}>
                  {t('boveda.retirar')}
                </Button>
              )}
            </Stack>
          )}
        </Box>
      ))}
    </Stack>
  );
}
