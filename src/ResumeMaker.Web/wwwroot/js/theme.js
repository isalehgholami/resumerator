// UI theme only (light/dark/system). Never touches resume output (rendered inside sandboxed iframes).
export function applyTheme(mode) {
  const root = document.documentElement;
  if (mode === 'light' || mode === 'dark') root.dataset.theme = mode;
  else delete root.dataset.theme;
}
