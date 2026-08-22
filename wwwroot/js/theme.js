/* =====================================================================
   theme.js — light / dark mode
   ===================================================================== */

const Theme = {
  init() {
    const saved = Store.get(CONFIG.STORAGE_KEYS.THEME);
    const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    const initial = saved || (prefersDark ? "dark" : "light");
    this.apply(initial);

    const toggleBtn = document.getElementById("theme-toggle");
    if (toggleBtn) {
      toggleBtn.addEventListener("click", () => {
        const current = document.documentElement.getAttribute("data-theme");
        this.apply(current === "dark" ? "light" : "dark");
      });
    }
  },
  apply(theme) {
    document.documentElement.setAttribute("data-theme", theme);
    Store.set(CONFIG.STORAGE_KEYS.THEME, theme);
  },
};
