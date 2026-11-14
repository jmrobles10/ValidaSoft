import { Stack, ToggleButton, ToggleButtonGroup } from '@mui/material';
import type { ContactoDelTerceroDto, SolicitudDeAccesoDePlataformaDto } from '@validasoft/api-client';
import { EsqueletoTarjeta, EstadoVacio } from '@validasoft/ui';
import { MailCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useDecidirSolicitudDeAcceso, useSolicitudesDeAcceso, useVerificarContactoDelTercero, type FiltroDeSolicitudes } from './apiAccesos';
import { DialogoDeSolicitudDeAcceso, type Decision } from './DialogoDeSolicitudDeAcceso';
import { TarjetaSolicitudDeAcceso } from './TarjetaSolicitudDeAcceso';

const FILTROS: readonly FiltroDeSolicitudes[] = ['cola', 'pendientes', 'resueltas', 'todas'];

/**
 * Solicitudes de acceso a terceros (decisión AV): cuando un correo que no es contacto de un tercero pide acceso y el tercero no tiene ningún contacto verificado, nadie del
 * tercero puede decidir y la solicitud espera aquí, con la evidencia —quién invitó, los contactos y su verificación, los correos del RUT y la Cámara—. Un operador aprueba o
 * rechaza (queda en la auditoría) o verifica a un contacto, con lo que la decisión vuelve a los contactos del tercero.
 */
export function PaginaSolicitudesDeAcceso() {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [filtro, setFiltro] = useState<FiltroDeSolicitudes>('cola');
  const [decision, setDecision] = useState<Decision | null>(null);
  const consulta = useSolicitudesDeAcceso(filtro);
  const decidir = useDecidirSolicitudDeAcceso();
  const verificar = useVerificarContactoDelTercero();

  const verificarContacto = (solicitud: SolicitudDeAccesoDePlataformaDto, contacto: ContactoDelTerceroDto) =>
    verificar.mutate(
      { terceroId: solicitud.terceroId, correo: contacto.correo },
      {
        onSuccess: () => mostrar(t('plataforma.accesos.contactoVerificado', { correo: contacto.correo })),
        onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      },
    );

  return (
    <>
      <EncabezadoPagina titulo={t('plataforma.accesos.titulo')} subtitulo={t('plataforma.accesos.subtitulo')} />
      <ToggleButtonGroup
        exclusive
        size="small"
        value={filtro}
        onChange={(_, valor: FiltroDeSolicitudes | null) => valor && setFiltro(valor)}
        aria-label={t('plataforma.accesos.filtro')}
        sx={{ mb: 2 }}
      >
        {FILTROS.map((f) => (
          <ToggleButton key={f} value={f}>
            {t(`plataforma.accesos.filtros.${f}`)}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
      {consulta.isPending ? (
        <Stack spacing={1.5}>
          <EsqueletoTarjeta alto={180} />
          <EsqueletoTarjeta alto={180} />
        </Stack>
      ) : consulta.isError ? (
        <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
      ) : consulta.data.length === 0 ? (
        <EstadoVacio icono={MailCheck} titulo={t(`plataforma.accesos.vacio.${filtro}`)} texto={t('plataforma.accesos.vacioAyuda')} />
      ) : (
        <Stack component="ul" spacing={1.5} sx={{ p: 0, m: 0, listStyle: 'none' }} aria-label={t('plataforma.accesos.lista')}>
          {consulta.data.map((s) => (
            <TarjetaSolicitudDeAcceso
              key={s.id}
              solicitud={s}
              onDecidir={(aprobar) => setDecision({ solicitud: s, aprobar })}
              onVerificar={(contacto) => verificarContacto(s, contacto)}
              verificando={verificar.isPending}
            />
          ))}
        </Stack>
      )}
      {decision && (
        <DialogoDeSolicitudDeAcceso
          decision={decision}
          ocupado={decidir.isPending}
          error={decidir.isError ? mensajeDeError(decidir.error, t) : null}
          onConfirmar={(motivo) =>
            decidir.mutate(
              { id: decision.solicitud.id, aprobar: decision.aprobar, motivo },
              {
                onSuccess: () => {
                  mostrar(t(decision.aprobar ? 'plataforma.accesos.aprobada' : 'plataforma.accesos.rechazada', { correo: decision.solicitud.correo }));
                  setDecision(null);
                },
              },
            )
          }
          onCerrar={() => {
            decidir.reset();
            setDecision(null);
          }}
        />
      )}
    </>
  );
}
