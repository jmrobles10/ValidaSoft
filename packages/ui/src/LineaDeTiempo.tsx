import { alpha, Box, Stack, Typography, useTheme } from '@mui/material';
import { Bot, ServerCog, User, type LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { claveDia, formatearDiaLargo, formatearHora, ZONA_HORARIA_POR_DEFECTO } from './formato';
import type { TonoEstado } from './tema';

export type TipoActor = 'humano' | 'tercero' | 'sistema' | 'integracion';

export interface EventoTimeline {
  id: string;
  /** Título legible del evento ("Vínculo bloqueado"). */
  titulo: string;
  fecha: string | Date;
  actor: { nombre: string; tipo: TipoActor };
  /** Sistema integrado desde el que se actuó (ADR-0011), p. ej. SRM. */
  sistema?: string | null;
  /** Motivo, justificación o comentario. */
  detalle?: ReactNode;
  antes?: string | null;
  despues?: string | null;
  tono?: TonoEstado;
}

export interface GrupoDia {
  clave: string;
  etiqueta: string;
  eventos: EventoTimeline[];
}

/**
 * Agrupa por día calendario en la zona del tenant, del más reciente al más antiguo. El orden es estable: eventos con
 * la misma hora conservan el orden recibido (pásalos del más reciente al más antiguo).
 */
export function agruparPorDia(eventos: readonly EventoTimeline[], zonaHoraria = ZONA_HORARIA_POR_DEFECTO): GrupoDia[] {
  const ordenados = [...eventos].sort((a, b) => new Date(b.fecha).getTime() - new Date(a.fecha).getTime());
  const grupos: GrupoDia[] = [];
  for (const evento of ordenados) {
    const clave = claveDia(evento.fecha, zonaHoraria);
    const ultimo = grupos.at(-1);
    if (ultimo?.clave === clave) {
      ultimo.eventos.push(evento);
    } else {
      grupos.push({ clave, etiqueta: formatearDiaLargo(evento.fecha, zonaHoraria), eventos: [evento] });
    }
  }
  return grupos;
}

const ICONO_ACTOR: Record<TipoActor, LucideIcon> = {
  humano: User,
  tercero: User,
  sistema: Bot,
  integracion: ServerCog,
};

export interface LineaDeTiempoProps {
  eventos: readonly EventoTimeline[];
  zonaHoraria?: string;
  vacio?: ReactNode;
}

/** Historial agrupado por día: quién (persona, tercero, `Sistema (IA)` o integración), desde qué sistema y antes/después. */
export function LineaDeTiempo({ eventos, zonaHoraria = ZONA_HORARIA_POR_DEFECTO, vacio = 'Sin eventos todavía.' }: LineaDeTiempoProps) {
  const tema = useTheme();
  const grupos = agruparPorDia(eventos, zonaHoraria);

  if (grupos.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        {vacio}
      </Typography>
    );
  }

  return (
    <Stack spacing={3} component="section" aria-label="Historial">
      {grupos.map((grupo) => (
        <Box key={grupo.clave}>
          <Typography variant="caption" color="text.secondary" component="h4" sx={{ mb: 1 }}>
            {grupo.etiqueta}
          </Typography>
          <Box component="ol" sx={{ listStyle: 'none', m: 0, p: 0 }}>
            {grupo.eventos.map((evento, indice) => {
              const color = tema.palette.estado[evento.tono ?? 'enCurso'];
              const Icono = ICONO_ACTOR[evento.actor.tipo];
              const esUltimo = indice === grupo.eventos.length - 1;
              return (
                <Box component="li" key={evento.id} sx={{ display: 'flex', gap: 1.5 }}>
                  <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', pt: 0.5 }}>
                    <Box
                      sx={{
                        width: 10,
                        height: 10,
                        borderRadius: '50%',
                        backgroundColor: color,
                        boxShadow: `0 0 0 4px ${alpha(color, 0.16)}`,
                      }}
                    />
                    {!esUltimo && <Box sx={{ flex: 1, width: '1px', backgroundColor: 'divider', my: 0.5 }} />}
                  </Box>
                  <Box sx={{ pb: esUltimo ? 0 : 2, minWidth: 0, flex: 1 }}>
                    <Stack direction="row" spacing={1} alignItems="baseline" justifyContent="space-between">
                      <Typography variant="body1">{evento.titulo}</Typography>
                      <Typography variant="mono" color="text.secondary" sx={{ flexShrink: 0 }}>
                        {formatearHora(evento.fecha, zonaHoraria)}
                      </Typography>
                    </Stack>
                    <Stack direction="row" spacing={0.75} alignItems="center" sx={{ color: 'text.secondary', mt: 0.25 }}>
                      <Icono size={13} strokeWidth={1.75} aria-hidden />
                      <Typography variant="caption">
                        {evento.actor.nombre}
                        {evento.sistema ? ` · desde ${evento.sistema}` : ''}
                      </Typography>
                    </Stack>
                    {(evento.antes ?? evento.despues) && (
                      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                        {evento.antes ?? '—'} → {evento.despues ?? '—'}
                      </Typography>
                    )}
                    {evento.detalle && (
                      <Typography variant="body2" sx={{ mt: 0.5 }}>
                        {evento.detalle}
                      </Typography>
                    )}
                  </Box>
                </Box>
              );
            })}
          </Box>
        </Box>
      ))}
    </Stack>
  );
}
