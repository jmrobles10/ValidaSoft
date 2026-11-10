import { Alert, Button, Checkbox, FormControl, FormControlLabel, FormGroup, FormHelperText, FormLabel, Paper, Skeleton, Typography } from '@mui/material';
import type { DocumentoBovedaDto, SoporteSeccionDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { useBoveda } from '../shared/api';
import { documentosDeSoporte, SOPORTE_FICHA, type DefinicionSeccion } from './secciones';

interface Props {
  definicion: DefinicionSeccion;
  soportes: readonly SoporteSeccionDto[];
  onCambiar: (soportes: SoporteSeccionDto[]) => void;
  /** «Falta el soporte del cambio» (FM-20), tal como lo devolvió el servidor. */
  faltaSoporte: string | null;
  /** Error 422 del soporte (`soporte-invalido`, `soporte-tipo-invalido`…). */
  error: string | null;
  /** Ruta de la sección, para volver después de subir un soporte. */
  volver: string;
  deshabilitado: boolean;
}

interface OpcionSoporte {
  soporte: SoporteSeccionDto;
  etiqueta: string;
}

const mismo = (a: SoporteSeccionDto, b: SoporteSeccionDto) => a.documentoId === b.documentoId && a.version === b.version;

/**
 * «Documento que respalda esta información»: los documentos globales de la bóveda del tercero de los tipos que admite la
 * sección (FM-20), en su versión vigente, más los soportes que ya tenía la versión guardada. «Subir un soporte» usa la
 * carga de siempre con el canónico `SOPORTE_FICHA` y vuelve a la sección.
 */
export function SelectorSoporte({ definicion, soportes, onCambiar, faltaSoporte, error, volver, deshabilitado }: Props) {
  const { t } = useTranslation();
  const boveda = useBoveda();
  const documentos = boveda.data?.documentos ?? [];
  const tipoDe = (documentoId: string) => documentos.find((d) => d.id === documentoId)?.tipo.nombre ?? null;

  const vigentes: OpcionSoporte[] = documentosDeSoporte(documentos, definicion).flatMap((d: DocumentoBovedaDto) =>
    d.versionActual
      ? [{ soporte: { documentoId: d.id, version: d.versionActual.n }, etiqueta: t('ficha.soporte.opcion', { tipo: d.tipo.nombre, n: String(d.versionActual.n), fecha: formatearFecha(d.versionActual.cargadaEn) }) }]
      : [],
  );
  // Un soporte guardado que ya no es la versión vigente (o de un documento que no aparece) sigue elegible.
  const anteriores: OpcionSoporte[] = soportes
    .filter((s) => !vigentes.some((o) => mismo(o.soporte, s)))
    .map((s) => {
      const tipo = tipoDe(s.documentoId);
      return { soporte: s, etiqueta: tipo ? t('ficha.soporte.anterior', { tipo, n: String(s.version) }) : t('ficha.soporte.otroDocumento', { n: String(s.version) }) };
    });
  const opciones = [...vigentes, ...anteriores];

  const alternar = (opcion: OpcionSoporte, marcado: boolean) =>
    onCambiar(marcado ? [...soportes, opcion.soporte] : soportes.filter((s) => !mismo(s, opcion.soporte)));

  const subir = `/boveda/subir?tipo=${SOPORTE_FICHA}&nombre=${encodeURIComponent(t('ficha.soporte.nombreCanonico'))}&volver=${encodeURIComponent(volver)}`;
  const mensaje = error ?? faltaSoporte;

  return (
    <Paper variant="outlined" sx={{ p: { xs: 1.5, sm: 2 } }}>
      <FormControl component="fieldset" error={Boolean(mensaje)} disabled={deshabilitado} fullWidth>
        <FormLabel component="legend" sx={{ fontWeight: 600, color: 'text.primary' }}>
          {t('ficha.soporte.titulo')}
        </FormLabel>
        <FormHelperText sx={{ mx: 0, mb: 1 }}>{definicion.soportes === null ? t('ficha.soporte.ayudaOpcional') : t('ficha.soporte.ayuda')}</FormHelperText>
        {boveda.isPending ? (
          <Skeleton variant="rounded" height={44} />
        ) : opciones.length === 0 ? (
          <Typography sx={{ color: 'text.secondary', fontSize: 14, mb: 1 }}>{t('ficha.soporte.ninguno')}</Typography>
        ) : (
          <FormGroup>
            {opciones.map((o) => (
              <FormControlLabel
                key={`${o.soporte.documentoId}-${String(o.soporte.version)}`}
                control={<Checkbox checked={soportes.some((s) => mismo(s, o.soporte))} onChange={(e) => alternar(o, e.target.checked)} />}
                label={o.etiqueta}
                sx={{ minHeight: 44, alignItems: 'center' }}
              />
            ))}
          </FormGroup>
        )}
        {mensaje && (
          <Alert severity={error ? 'error' : 'warning'} sx={{ mt: 1 }}>
            {mensaje}
          </Alert>
        )}
      </FormControl>
      <Button component={RouterLink} to={subir} variant="text" startIcon={<Upload size={18} aria-hidden />} sx={{ mt: 1, minHeight: 44 }}>
        {t('ficha.soporte.subir')}
      </Button>
      <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('ficha.soporte.subirAyuda')}</Typography>
    </Paper>
  );
}
