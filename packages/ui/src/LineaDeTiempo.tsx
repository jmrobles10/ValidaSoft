import { Box, Stack, Typography } from '@mui/material';
import { ArrowRight, Plug } from 'lucide-react';
import type { ReactNode } from 'react';
import { ChipEstado } from './ChipEstado';
import { AvatarActor, type TipoAvatar } from './datos';
import type { Estado } from './estados';
import { claveDia, formatearFecha, formatearHora, ZONA_HORARIA_POR_DEFECTO } from './formato';
import { EstadoVacio } from './patrones';

export type TipoActor = TipoAvatar;

export interface EventoTimeline {
  id: string;
  /** Título legible del evento ("Vínculo bloqueado"). */
  titulo: string;
  fecha: string | Date;
  actor: { nombre: string; tipo: TipoActor };
  /** Sistema integrado desde el que se actuó (ADR-0011): se muestra "desde SRM". */
  sistema?: string | null;
  /** Dato del evento ("A contacto@concretos.co, vence el 09/11/2026"). */
  detalle?: ReactNode;
  /** Motivo o justificación escritos por una persona: van citados. */
  motivo?: string | null;
  antes?: Estado | null;
  despues?: Estado | null;
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
      grupos.push({ clave, etiqueta: formatearFecha(evento.fecha, zonaHoraria), eventos: [evento] });
    }
  }
  return grupos;
}

export interface LineaDeTiempoProps {
  eventos: readonly EventoTimeline[];
  zonaHoraria?: string;
  vacio?: string;
}

/** Historial agrupado por día: quién (persona, tercero, `Sistema (IA)` o integración), desde qué sistema, antes → después y motivo. */
export function LineaDeTiempo({ eventos, zonaHoraria = ZONA_HORARIA_POR_DEFECTO, vacio = 'Sin eventos todavía.' }: LineaDeTiempoProps) {
  const grupos = agruparPorDia(eventos, zonaHoraria);

  if (grupos.length === 0) return <EstadoVacio compacto titulo={vacio} />;

  return (
    <Stack spacing={2.5}>
      {grupos.map((grupo) => (
        <Box component="section" key={grupo.clave} aria-label={grupo.etiqueta}>
          <Typography
            component="h3"
            sx={{ mb: 1, color: 'text.secondary', fontSize: 12, lineHeight: '16px', fontWeight: 600, letterSpacing: '0.04em', fontVariantNumeric: 'tabular-nums' }}
          >
            {grupo.etiqueta}
          </Typography>
          <Box
            component="ol"
            sx={{
              position: 'relative',
              display: 'flex',
              flexDirection: 'column',
              gap: 2,
              m: 0,
              p: 0,
              listStyle: 'none',
              '&::before': { content: '""', position: 'absolute', left: 13, top: 8, bottom: 8, width: '1px', bgcolor: 'divider' },
            }}
          >
            {grupo.eventos.map((evento) => (
              <Box component="li" key={evento.id} sx={{ position: 'relative', display: 'flex', gap: 1.5 }}>
                <Box sx={{ position: 'relative', zIndex: 1, borderRadius: '50%', boxShadow: (t) => `0 0 0 3px ${t.palette.background.paper}`, alignSelf: 'flex-start' }}>
                  <AvatarActor tipo={evento.actor.tipo} nombre={evento.actor.nombre} />
                </Box>
                <Stack spacing={0.5} sx={{ flex: 1, minWidth: 0 }}>
                  <Typography sx={{ fontWeight: 600 }}>{evento.titulo}</Typography>
                  <Typography variant="body2" sx={{ color: 'text.secondary' }} component="p">
                    {evento.actor.nombre}
                    {evento.sistema && (
                      <>
                        {' · '}
                        <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: '3px', verticalAlign: 'middle' }}>
                          <Plug size={12} strokeWidth={1.75} aria-hidden />
                          desde {evento.sistema}
                        </Box>
                      </>
                    )}
                    {' · '}
                    <Typography component="span" variant="monoTabla">
                      {formatearHora(evento.fecha, zonaHoraria)}
                    </Typography>
                  </Typography>
                  {(evento.antes ?? evento.despues) && (
                    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, flexWrap: 'wrap', color: 'text.secondary' }}>
                      {evento.antes && <ChipEstado estado={evento.antes} />}
                      {evento.antes && evento.despues && <ArrowRight size={14} strokeWidth={1.75} aria-label="pasó a" />}
                      {evento.despues && <ChipEstado estado={evento.despues} />}
                    </Box>
                  )}
                  {evento.detalle && (
                    <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                      {evento.detalle}
                    </Typography>
                  )}
                  {evento.motivo && (
                    <Box
                      component="blockquote"
                      sx={{ m: 0, px: 1.25, py: 0.75, borderRadius: (t) => `${String(t.radio.sm)}px`, bgcolor: 'superficie.nivel2', fontSize: 13, lineHeight: '18px' }}
                    >
                      {evento.motivo}
                    </Box>
                  )}
                </Stack>
              </Box>
            ))}
          </Box>
        </Box>
      ))}
    </Stack>
  );
}
