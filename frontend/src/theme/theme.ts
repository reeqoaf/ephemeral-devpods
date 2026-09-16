import { createTheme, type PaletteMode } from '@mui/material/styles'

export function getTheme(mode: PaletteMode) {
  const isDark = mode === 'dark'

  return createTheme({
    palette: {
      mode,
      primary: { main: isDark ? '#8b8cf6' : '#5b5bd6' },
      secondary: { main: '#22d3ee' },
      background: isDark
        ? { default: '#0b0d12', paper: '#12151c' }
        : { default: '#f7f7fb', paper: '#ffffff' },
    },
    shape: { borderRadius: 12 },
    typography: {
      fontFamily: [
        'Inter',
        'system-ui',
        '-apple-system',
        'Segoe UI',
        'Roboto',
        'sans-serif',
      ].join(','),
      h4: { fontWeight: 700, letterSpacing: '-0.02em' },
    },
    components: {
      MuiPaper: {
        styleOverrides: {
          root: {
            backgroundImage: 'none',
          },
        },
      },
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: { textTransform: 'none', fontWeight: 600, borderRadius: 10 },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: { fontWeight: 600 },
        },
      },
      MuiCard: {
        styleOverrides: {
          root: {
            border: `1px solid ${isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.06)'}`,
          },
        },
      },
    },
  })
}
