import { Box, Button, Drawer, IconButton, Link, Stack, Typography } from '@mui/material';
import type { RegistroAuditoriaDto } from '@validasoft/api-client';
import { Dato, RejillaDatos } from '@validasoft/ui';
import { Check, Copy, X } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { DiffAuditoria } from './DiffAuditoria';
import { formatearMomento, textoAccion, textoActor, textoEntidad } from './modelo';

const Mono = ({ children }: { children: string }) => (
  <Typography component="span" variant="mono" sx={{ overflowWrap: 'anywhere' }}>
    {children}
  </Typography>
);

/** `correlationId` con botón «Copiar» (para cruzarlo con los registros técnicos). */
function CorrelationId({ valor }: { valor: string }) {
  const { t } = useTranslation();
  const [copiado, setCopiado] = useState(false);
  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(valor);
      setCopiado(true);
    } catch {
      setCopiado(false);
    }
  };
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
      <Mono>{valor}</Mono>
      <Button variant="text" size="small" startIcon={copiado ? <Check size={14} aria-hidden /> : <Copy size={14} aria-hidden />} onClick={() => void copiar()}>
        {copiado ? t('auditoria.detalle.copiado') : t('auditoria.detalle.copiar')}
      </Button>
    </Stack>
  );
}

/** Lo propio de un acceso: qué se descargó, por qué, desde dónde y el vínculo al que pertenece. */
function DatosAcceso({ registro, empresaId }: { registro: RegistroAuditoriaDto; empresaId: string }) {
  const { t } = useTranslation();
  const noRegistrado = t('auditoria.detalle.noRegistrado');
  return (
    <RejillaDatos columnas={2}>
      <Dato etiqueta={registro.entidad.tipo === 'Certificado' ? t('auditoria.detalle.certificado') : t('auditoria.detalle.documento')} ancho>
        {registro.entidad.nombre ?? registro.entidad.id}
      </Dato>
      <Dato etiqueta={t('auditoria.detalle.motivo')} ancho>
        {registro.detalle ?? t('auditoria.detalle.sinMotivo')}
      </Dato>
      <Dato etiqueta={t('auditoria.detalle.ip')}>{registro.ip ? <Mono>{registro.ip}</Mono> : noRegistrado}</Dato>
      <Dato etiqueta={t('auditoria.detalle.navegador')} ancho>
        {registro.userAgent ? <Mono>{registro.userAgent}</Mono> : noRegistrado}
      </Dato>
      {registro.vinculoId && (
        <Dato etiqueta={t('nav.vinculos')}>
          <Link component={RouterLink} to={`/e/${empresaId}/vinculos/${registro.vinculoId}`}>
            {t('auditoria.detalle.verVinculo')}
          </Link>
        </Dato>
      )}
    </RejillaDatos>
  );
}

/** Lo propio de un ingreso, una passkey o una sesión (ING-6): qué pasó, desde qué IP y con qué navegador. */
function DatosIdentidad({ registro }: { registro: RegistroAuditoriaDto }) {
  const { t } = useTranslation();
  const noRegistrado = t('auditoria.detalle.noRegistrado');
  return (
    <RejillaDatos columnas={2}>
      <Dato etiqueta={t('auditoria.detalle.queSePaso')} ancho>
        {registro.detalle ?? t('auditoria.detalle.sinMotivo')}
      </Dato>
      <Dato etiqueta={t('auditoria.detalle.ip')}>{registro.ip ? <Mono>{registro.ip}</Mono> : noRegistrado}</Dato>
      <Dato etiqueta={t('auditoria.detalle.navegador')} ancho>
        {registro.userAgent ? <Mono>{registro.userAgent}</Mono> : noRegistrado}
      </Dato>
    </RejillaDatos>
  );
}

/** Panel lateral con el detalle de un registro de auditoría. */
export function DetalleAuditoria({ registro, empresaId, onCerrar }: { registro: RegistroAuditoriaDto; empresaId: string; onCerrar: () => void }) {
  const { t } = useTranslation();
  const entidad = textoEntidad(registro.entidad.tipo, t);
  const esAcceso = registro.tipo === 'Acceso';
  const esIdentidad = registro.tipo === 'Identidad';
  return (
    <Drawer
      anchor="right"
      open
      onClose={onCerrar}
      slotProps={{ paper: { role: 'dialog', 'aria-modal': true, 'aria-labelledby': 'titulo-auditoria', sx: { width: 560, maxWidth: '100%' } } }}
    >
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', px: 2.5, py: 2, borderBottom: 1, borderColor: 'divider' }}>
        <Typography id="titulo-auditoria" variant="h4" component="h2">
          {t('auditoria.detalle.titulo', { accion: textoAccion(registro.accion, t), entidad })}
        </Typography>
        <IconButton aria-label={t('auditoria.detalle.cerrar')} onClick={onCerrar}>
          <X size={18} strokeWidth={1.75} />
        </IconButton>
      </Stack>
      <Stack spacing={2.5} sx={{ p: 2.5, flex: 1, overflowY: 'auto' }}>
        <RejillaDatos columnas={2}>
          <Dato etiqueta={t('auditoria.detalle.fecha')}>
            <Mono>{formatearMomento(registro.fecha)}</Mono>
          </Dato>
          <Dato etiqueta={t('auditoria.detalle.actor')}>
            {textoActor(registro.actor, t)}
            {registro.actor.sistema && ` · ${t('auditoria.actores.desdeSistema', { sistema: registro.actor.sistema })}`}
          </Dato>
          {!esAcceso && <Dato etiqueta={t('auditoria.detalle.entidad')}>{registro.entidad.nombre ? `${entidad} · ${registro.entidad.nombre}` : entidad}</Dato>}
          {!esAcceso && (
            <Dato etiqueta={t('auditoria.detalle.alcance')}>{registro.empresaId === null ? t('auditoria.alcance.grupo') : t('auditoria.alcance.empresa')}</Dato>
          )}
          {!esAcceso && (
            <Dato etiqueta={t('auditoria.detalle.idEntidad')} ancho>
              <Mono>{registro.entidad.id}</Mono>
            </Dato>
          )}
        </RejillaDatos>

        {esAcceso ? (
          <DatosAcceso registro={registro} empresaId={empresaId} />
        ) : esIdentidad ? (
          <DatosIdentidad registro={registro} />
        ) : (
          <Box component="section" aria-labelledby="titulo-cambios">
            <Typography id="titulo-cambios" variant="h5" component="h3" sx={{ mb: 1 }}>
              {t('auditoria.detalle.cambios')}
            </Typography>
            <DiffAuditoria diff={registro.diff} />
          </Box>
        )}

        {(registro.justificacion ?? registro.correlationId) && (
          <RejillaDatos columnas={2}>
            {registro.justificacion && (
              <Dato etiqueta={t('auditoria.detalle.justificacion')} ancho>
                {registro.justificacion}
              </Dato>
            )}
            {registro.correlationId && (
              <Dato etiqueta={t('auditoria.detalle.correlationId')} ancho>
                <CorrelationId valor={registro.correlationId} />
              </Dato>
            )}
          </RejillaDatos>
        )}
      </Stack>
    </Drawer>
  );
}
