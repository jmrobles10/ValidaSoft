import { Avatar, Box, ListItemIcon, ListItemText, MenuItem, Select, Stack, Typography, type SelectChangeEvent } from '@mui/material';
import { Building2 } from 'lucide-react';
import { useId } from 'react';

export interface EmpresaOpcion {
  id: string;
  nombre: string;
  /** NIT u otra identificación visible (docs/06 §6: "SelectorEmpresa siempre visible con logo y NIT"). */
  identificacion?: string | null;
  logoUrl?: string | null;
}

export interface SelectorEmpresaProps {
  empresas: readonly EmpresaOpcion[];
  /** Id de la empresa activa; `null` mientras no hay una elegida. */
  actual: string | null;
  onCambiar: (empresaId: string) => void;
  etiqueta?: string;
  deshabilitado?: boolean;
}

function Logo({ empresa }: { empresa: EmpresaOpcion }) {
  return (
    <Avatar
      src={empresa.logoUrl ?? undefined}
      alt=""
      variant="rounded"
      sx={{ width: 24, height: 24, bgcolor: 'superficie.nivel2', color: 'text.secondary' }}
    >
      <Building2 size={14} strokeWidth={1.75} aria-hidden />
    </Avatar>
  );
}

function Descripcion({ empresa }: { empresa: EmpresaOpcion }) {
  return (
    <ListItemText
      primary={empresa.nombre}
      secondary={empresa.identificacion ? `NIT ${empresa.identificacion}` : null}
      slotProps={{
        primary: { noWrap: true, variant: 'body2' },
        secondary: { noWrap: true, variant: 'caption' },
      }}
      sx={{ my: 0 }}
    />
  );
}

/**
 * Empresa activa (razón social) del AppShell: toda pantalla de operación trabaja en una empresa (ADR-0008).
 * Con una sola empresa se muestra fija, sin desplegable.
 */
export function SelectorEmpresa({ empresas, actual, onCambiar, etiqueta = 'Empresa', deshabilitado = false }: SelectorEmpresaProps) {
  const idEtiqueta = useId();
  const seleccionada = empresas.find((e) => e.id === actual) ?? null;

  if (empresas.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        Sin empresas disponibles
      </Typography>
    );
  }

  const unica = empresas.length === 1 ? empresas[0] : undefined;
  if (unica) {
    return (
      <Stack direction="row" spacing={1} alignItems="center" aria-label={`${etiqueta}: ${unica.nombre}`} sx={{ minWidth: 0 }}>
        <Logo empresa={unica} />
        <Descripcion empresa={unica} />
      </Stack>
    );
  }

  return (
    <Box sx={{ minWidth: 220, maxWidth: 320 }}>
      <Typography id={idEtiqueta} variant="caption" color="text.secondary" component="div" sx={{ mb: 0.5 }}>
        {etiqueta}
      </Typography>
      <Select
        size="small"
        fullWidth
        value={seleccionada?.id ?? ''}
        displayEmpty
        disabled={deshabilitado}
        onChange={(evento: SelectChangeEvent) => {
          if (evento.target.value && evento.target.value !== actual) onCambiar(evento.target.value);
        }}
        inputProps={{ 'aria-labelledby': idEtiqueta }}
        renderValue={(valor) => {
          const empresa = empresas.find((e) => e.id === valor);
          if (!empresa) return <Typography color="text.secondary">Elige una empresa</Typography>;
          return (
            <Stack direction="row" spacing={1} alignItems="center" sx={{ minWidth: 0 }}>
              <Logo empresa={empresa} />
              <Typography variant="body2" noWrap>
                {empresa.nombre}
              </Typography>
            </Stack>
          );
        }}
      >
        {empresas.map((empresa) => (
          <MenuItem key={empresa.id} value={empresa.id}>
            <ListItemIcon sx={{ minWidth: 36 }}>
              <Logo empresa={empresa} />
            </ListItemIcon>
            <Descripcion empresa={empresa} />
          </MenuItem>
        ))}
      </Select>
    </Box>
  );
}
