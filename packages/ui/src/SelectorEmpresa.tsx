import { Avatar, Box, ButtonBase, InputBase, ListItemIcon, ListItemText, Menu, MenuItem, Typography } from '@mui/material';
import { Check, ChevronsUpDown, Search } from 'lucide-react';
import { useId, useMemo, useState } from 'react';
import { iniciales } from './datos';

export interface EmpresaOpcion {
  id: string;
  nombre: string;
  /** NIT u otra identificación visible, con su dígito de verificación. */
  identificacion?: string | null;
  logoUrl?: string | null;
  /** Prefijo de folio (NORTE): se muestra en el menú. */
  prefijo?: string | null;
}

export interface SelectorEmpresaProps {
  empresas: readonly EmpresaOpcion[];
  /** Id de la empresa activa; `null` mientras no hay una elegida. */
  actual: string | null;
  onCambiar: (empresaId: string) => void;
  etiqueta?: string;
  deshabilitado?: boolean;
}

/** Con más empresas que esto, el menú trae buscador. */
const UMBRAL_BUSQUEDA = 5;

/** Logo de la empresa a 24 px; sin logo, sus iniciales sobre el tono neutro. */
export function LogoEmpresa({ empresa, tamano = 24 }: { empresa: EmpresaOpcion; tamano?: number }) {
  return (
    <Avatar
      src={empresa.logoUrl ?? undefined}
      alt=""
      variant="rounded"
      sx={{ width: tamano, height: tamano, borderRadius: '7px', bgcolor: 'estado.neutro.solido', color: '#FFFFFF', fontSize: Math.round(tamano * 0.38), fontWeight: 700 }}
    >
      {iniciales(empresa.nombre)}
    </Avatar>
  );
}

function Textos({ empresa }: { empresa: EmpresaOpcion }) {
  return (
    <Box component="span" sx={{ display: 'flex', flexDirection: 'column', minWidth: 0, textAlign: 'left' }}>
      <Typography component="span" noWrap sx={{ fontSize: 13, lineHeight: '16px', fontWeight: 600 }}>
        {empresa.nombre}
      </Typography>
      {empresa.identificacion && (
        <Typography component="span" noWrap color="text.secondary" sx={{ fontSize: 12, lineHeight: '14px' }}>
          NIT {empresa.identificacion}
        </Typography>
      )}
    </Box>
  );
}

/**
 * Empresa activa con logo, razón social y NIT: toda pantalla de operación trabaja dentro de una empresa (ADR-0008).
 * Con una sola empresa se muestra fija, sin menú.
 */
export function SelectorEmpresa({ empresas, actual, onCambiar, etiqueta = 'Empresa', deshabilitado = false }: SelectorEmpresaProps) {
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const [busqueda, setBusqueda] = useState('');
  const idMenu = useId();
  const seleccionada = empresas.find((e) => e.id === actual) ?? empresas[0] ?? null;
  const visibles = useMemo(() => {
    const q = busqueda.trim().toLowerCase();
    return q ? empresas.filter((e) => `${e.nombre} ${e.identificacion ?? ''} ${e.prefijo ?? ''}`.toLowerCase().includes(q)) : empresas;
  }, [empresas, busqueda]);

  if (!seleccionada) {
    return (
      <Typography variant="body2" color="text.secondary">
        Sin empresas disponibles
      </Typography>
    );
  }

  const textoAccesible = `${etiqueta}: ${seleccionada.nombre}${seleccionada.identificacion ? `, NIT ${seleccionada.identificacion}` : ''}`;
  const caja = { display: 'inline-flex', alignItems: 'center', gap: 1, height: 36, maxWidth: 300, minWidth: 0, pl: '5px', pr: 1 } as const;

  if (empresas.length === 1) {
    return (
      <Box aria-label={textoAccesible} role="group" sx={{ ...caja, pl: 0 }}>
        <LogoEmpresa empresa={seleccionada} />
        <Textos empresa={seleccionada} />
      </Box>
    );
  }

  const cerrar = () => {
    setAncla(null);
    setBusqueda('');
  };
  const elegir = (id: string) => {
    cerrar();
    if (id !== actual) onCambiar(id);
  };

  return (
    <>
      <ButtonBase
        aria-label={textoAccesible}
        aria-haspopup="menu"
        aria-expanded={ancla ? 'true' : undefined}
        aria-controls={ancla ? idMenu : undefined}
        disabled={deshabilitado}
        onClick={(e) => setAncla(e.currentTarget)}
        sx={{
          ...caja,
          border: 1,
          borderColor: 'divider',
          borderRadius: (t) => `${String(t.radio.sm)}px`,
          bgcolor: 'background.paper',
          color: 'text.primary',
          '&:hover': { borderColor: 'borde.control' },
        }}
      >
        <LogoEmpresa empresa={seleccionada} />
        <Textos empresa={seleccionada} />
        <Box component="span" sx={{ color: 'text.secondary', display: 'flex', flex: 'none' }}>
          <ChevronsUpDown size={16} strokeWidth={1.75} aria-hidden />
        </Box>
      </ButtonBase>
      <Menu
        id={idMenu}
        anchorEl={ancla}
        open={Boolean(ancla)}
        onClose={cerrar}
        disableAutoFocusItem={empresas.length > UMBRAL_BUSQUEDA}
        slotProps={{ paper: { sx: { width: 320 } } }}
      >
        {empresas.length > UMBRAL_BUSQUEDA && (
          <Box
            onKeyDown={(e) => e.stopPropagation()}
            sx={{ display: 'flex', alignItems: 'center', gap: 1, mx: 0.5, mb: 0.5, px: 1.25, height: 32, border: 1, borderColor: 'borde.control', borderRadius: (t) => `${String(t.radio.sm)}px`, color: 'text.secondary' }}
          >
            <Search size={16} strokeWidth={1.75} aria-hidden />
            <InputBase
              // Con buscador, el foco va al campo (no al primer ítem) apenas se abre el menú.
              inputRef={(nodo: HTMLInputElement | null) => {
                if (nodo) requestAnimationFrame(() => nodo.focus());
              }}
              value={busqueda}
              onChange={(e) => setBusqueda(e.target.value)}
              placeholder="Buscar empresa"
              inputProps={{ 'aria-label': 'Buscar empresa' }}
              sx={{ flex: 1, fontSize: 14 }}
            />
          </Box>
        )}
        {visibles.map((empresa) => (
          <MenuItem key={empresa.id} selected={empresa.id === seleccionada.id} onClick={() => elegir(empresa.id)}>
            <ListItemIcon>
              <LogoEmpresa empresa={empresa} />
            </ListItemIcon>
            <ListItemText
              primary={empresa.nombre}
              secondary={[empresa.identificacion ? `NIT ${empresa.identificacion}` : null, empresa.prefijo].filter(Boolean).join(' · ') || null}
              slotProps={{ primary: { noWrap: true, sx: { fontSize: 14 } }, secondary: { noWrap: true, sx: { fontSize: 12 } } }}
              sx={{ my: 0, minWidth: 0 }}
            />
            {empresa.id === seleccionada.id && (
              <Box component="span" sx={{ color: 'acento.main', display: 'flex' }}>
                <Check size={16} strokeWidth={1.75} aria-hidden />
              </Box>
            )}
          </MenuItem>
        ))}
        {visibles.length === 0 && (
          <MenuItem disabled>
            <ListItemText primary="Ninguna empresa coincide" />
          </MenuItem>
        )}
      </Menu>
    </>
  );
}
