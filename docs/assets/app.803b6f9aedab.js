(() => {
  'use strict';
  const root = document.documentElement;
  const systemTheme = matchMedia('(prefers-color-scheme: dark)');
  const themeButtons = document.querySelectorAll('[data-theme-mode]');
  let preference = root.dataset.themePreference || 'auto';
  function applyTheme() {
    const theme = preference === 'auto' ? (systemTheme.matches ? 'dark' : 'light') : preference;
    root.dataset.theme = theme;
    root.dataset.themePreference = preference;
    themeButtons.forEach(button => button.setAttribute('aria-pressed', String(button.dataset.themeMode === preference)));
    document.querySelector('meta[name="theme-color"]').content = theme === 'dark' ? '#0e1625' : '#f6f8f7';
  }
  themeButtons.forEach(button => button.addEventListener('click', () => {
    preference = button.dataset.themeMode;
    try { localStorage.setItem('reader-guard-theme', preference); } catch (_) {}
    applyTheme();
  }));
  systemTheme.addEventListener('change', () => { if (preference === 'auto') applyTheme(); });
  window.addEventListener('storage', event => {
    if (event.key === 'reader-guard-theme' || event.key === null) {
      preference = ['light', 'dark'].includes(event.newValue) ? event.newValue : 'auto';
      applyTheme();
    }
  });
  applyTheme();

  const menu = document.querySelector('.menu-toggle');
  const nav = document.getElementById('primary-nav');
  function closeMenu(returnFocus = false) {
    nav.classList.remove('is-open');
    menu.setAttribute('aria-expanded', 'false');
    if (returnFocus) menu.focus();
  }
  menu.addEventListener('click', () => {
    const open = menu.getAttribute('aria-expanded') !== 'true';
    menu.setAttribute('aria-expanded', String(open));
    nav.classList.toggle('is-open', open);
  });
  nav.querySelectorAll('a').forEach(link => link.addEventListener('click', () => closeMenu()));
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && menu.getAttribute('aria-expanded') === 'true') closeMenu(true);
  });
  document.addEventListener('click', event => {
    if (!event.target.closest('.site-header')) closeMenu();
  });
  matchMedia('(min-width: 851px)').addEventListener('change', () => closeMenu());

  const trigger = document.getElementById('demo-trigger');
  const progress = document.getElementById('demo-progress');
  const state = document.getElementById('demo-state');
  const pill = document.getElementById('demo-pill');
  const toast = document.getElementById('demo-toast');
  trigger.addEventListener('click', () => {
    if (trigger.disabled) return;
    trigger.disabled = true;
    toast.hidden = true;
    progress.classList.remove('running');
    state.textContent = '演示中 · 连续空闲计时';
    pill.textContent = '等待空闲';
    trigger.textContent = '正在演示，未操作任何真实设备…';
    requestAnimationFrame(() => requestAnimationFrame(() => progress.classList.add('running')));
    setTimeout(() => {
      state.textContent = '演示：已达到设定空闲时间，系统返回成功';
      pill.textContent = '已安全弹出';
      toast.hidden = false;
      trigger.disabled = false;
      trigger.textContent = '再体验一次 →';
    }, matchMedia('(prefers-reduced-motion: reduce)').matches ? 500 : 5100);
  });
})();
