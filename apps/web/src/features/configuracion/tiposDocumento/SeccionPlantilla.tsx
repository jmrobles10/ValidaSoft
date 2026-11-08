import { Alert, Box, Button, Stack, Typography } from '@mui/material';
import { formatearFecha } from '@validasoft/ui';
import { Download, FileUp, Trash2 } from 'lucide-react';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { MIMES_PLANTILLA, useAccionesPlantilla, usePlantilla } from './api';
import { megas } from './modelo';
import { Seccion } from './SeccionesTipo';

/** Plantilla descargable del tipo (PDF, DOCX o XLSX): el proveedor la descarga, la llena y la sube (TD-5). */
export function SeccionPlantilla({ tipoId }: { tipoId: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const consulta = usePlantilla(tipoId);
  const { subir, retirar, descargar } = useAccionesPlantilla(tipoId);
  const entrada = useRef<HTMLInputElement>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const vigente = consulta.data?.vigente ?? null;
  const anteriores = (consulta.data?.historial ?? []).filter((v) => !v.vigente);
  const ocupado = subir.isPending || retirar.isPending;
  const fallo = subir.error ?? retirar.error ?? descargar.error;

  const elegir = (archivo: File | undefined) => {
    setAviso(null);
    if (!archivo) return;
    if (!MIMES_PLANTILLA.some((m) => m === archivo.type)) {
      setAviso(t('tiposDocumento.plantilla.formatoNoAdmitido'));
      return;
    }
    subir.mutate(archivo, {
      onSuccess: (r) => mostrar(r.duplicado ? t('tiposDocumento.plantilla.duplicada') : t('tiposDocumento.plantilla.publicada', { version: String(r.plantilla.version) })),
    });
  };

  return (
    <Seccion titulo={t('tiposDocumento.secciones.plantilla')} ayuda={t('tiposDocumento.plantilla.ayuda')}>
      {(aviso ?? fallo) && <Alert severity="error">{aviso ?? mensajeDeError(fallo, t)}</Alert>}
      {vigente ? (
        <Stack direction="row" spacing={1.5} alignItems="center" sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, px: 1.5, py: 1 }}>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography sx={{ fontSize: 14, fontWeight: 500, overflowWrap: 'anywhere' }}>{vigente.nombreOriginal}</Typography>
            <Typography variant="body2" color="text.secondary">
              {t('tiposDocumento.plantilla.detalle', { version: String(vigente.version), peso: megas(vigente.tamanoBytes), fecha: formatearFecha(vigente.cargadaEn) })}
            </Typography>
          </Box>
          <Button size="small" variant="text" startIcon={<Download size={16} aria-hidden />} onClick={() => descargar.mutate(vigente.version)}>
            {t('tiposDocumento.plantilla.descargar')}
          </Button>
          <Button size="small" variant="text" color="error" startIcon={retirar.isPending ? <IconoCarga /> : <Trash2 size={16} aria-hidden />} disabled={ocupado} onClick={() => retirar.mutate(undefined, { onSuccess: () => mostrar(t('tiposDocumento.plantilla.retirada')) })}>
            {t('tiposDocumento.plantilla.retirar')}
          </Button>
        </Stack>
      ) : (
        <Typography variant="body2" color="text.secondary">
          {consulta.isPending ? t('comun.cargando') : t('tiposDocumento.plantilla.sinPlantilla')}
        </Typography>
      )}
      <Box>
        <input ref={entrada} hidden type="file" accept={MIMES_PLANTILLA.join(',')} onChange={(e) => { elegir(e.target.files?.[0]); e.target.value = ''; }} />
        <Button variant="outlined" startIcon={subir.isPending ? <IconoCarga /> : <FileUp size={16} aria-hidden />} disabled={ocupado} onClick={() => entrada.current?.click()}>
          {vigente ? t('tiposDocumento.plantilla.reemplazar') : t('tiposDocumento.plantilla.subir')}
        </Button>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
          {t('tiposDocumento.plantilla.formatos')}
        </Typography>
      </Box>
      {anteriores.length > 0 && (
        <Box component="details">
          <Typography component="summary" variant="body2" sx={{ cursor: 'pointer', color: 'text.secondary' }}>
            {t('tiposDocumento.plantilla.historial', { count: anteriores.length })}
          </Typography>
          <Stack component="ul" spacing={0.5} sx={{ m: 0, mt: 1, pl: 2.5 }}>
            {anteriores.map((v) => (
              <Typography component="li" variant="body2" key={v.version}>
                v{v.version} · {v.nombreOriginal} · {formatearFecha(v.cargadaEn)}
                {v.retiradaEn ? ` · ${t('tiposDocumento.plantilla.retiradaEl', { fecha: formatearFecha(v.retiradaEn) })}` : ''}{' '}
                <Button size="small" variant="text" onClick={() => descargar.mutate(v.version)}>
                  {t('tiposDocumento.plantilla.descargar')}
                </Button>
              </Typography>
            ))}
          </Stack>
        </Box>
      )}
    </Seccion>
  );
}
