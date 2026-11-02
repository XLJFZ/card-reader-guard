(() => {
  'use strict';
  const systemTheme = window.matchMedia('(prefers-color-scheme: dark)');
  const themeButtons = document.querySelectorAll('[data-theme-mode]');
  let preference = document.documentElement.dataset.themePreference || 'auto';
  const applyTheme = () => {
    const theme = preference === 'auto' ? (systemTheme.matches ? 'dark' : 'light') : preference;
    document.documentElement.dataset.theme = theme;
    document.documentElement.dataset.themePreference = preference;
    themeButtons.forEach(button => button.setAttribute('aria-pressed', String(button.dataset.themeMode === preference)));
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.content = theme === 'dark' ? '#0e1625' : '#f7f8f2';
  };
  themeButtons.forEach(button => button.addEventListener('click', () => {
    preference = button.dataset.themeMode;
    try { localStorage.setItem('reader-guard-theme', preference); } catch (_) {}
    applyTheme();
  }));
  systemTheme.addEventListener('change', () => { if (preference === 'auto') applyTheme(); });
  window.addEventListener('storage', event => {
    if (event.key === 'reader-guard-theme') {
      preference = ['light', 'dark'].includes(event.newValue) ? event.newValue : 'auto';
      applyTheme();
    }
  });
  applyTheme();
  const trigger = document.getElementById('demo-trigger');
  const progress = document.getElementById('demo-progress');
  const state = document.getElementById('demo-state');
  const pill = document.getElementById('demo-pill');
  const toast = document.getElementById('demo-toast');
  if (!trigger || !progress || !state || !pill || !toast) return;
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  trigger.addEventListener('click', () => {
    if (trigger.disabled) return;
    trigger.disabled = true;
    toast.hidden = true;
    progress.classList.remove('running');
    state.textContent = '演示中 · 连续空闲计时';
    pill.textContent = '等待空闲';
    trigger.textContent = '正在演示，未操作任何真实设备…';
    requestAnimationFrame(() => requestAnimationFrame(() => progress.classList.add('running')));
    window.setTimeout(() => {
      state.textContent = '演示：已达到设定空闲时间';
      pill.textContent = '已安全弹出';
      toast.hidden = false;
      trigger.disabled = false;
      trigger.textContent = '再体验一次 →';
    }, reducedMotion ? 500 : 5100);
  });
})();
