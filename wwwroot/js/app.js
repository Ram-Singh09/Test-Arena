/* =====================================================================
   app.js — init
   ===================================================================== */

let lastAuthExpiredToast = 0;
function handleAuthExpired() {
  Store.remove(CONFIG.STORAGE_KEYS.USER);
  Auth.updateNav();
  const now = Date.now();
  if (now - lastAuthExpiredToast > 3000) {
    showToast("Your session has expired — please log in again.", "error");
    lastAuthExpiredToast = now;
  }
  showView("view-login");
}

document.addEventListener("DOMContentLoaded", () => {
  Theme.init();
  ApiConfigModal.init();

  wireAuthForms();
  wireProfile();
  wireCreateRoom();
  HostPanel.wire();
  wireJoinRoom();
  wireTestFlow();
  wireLeaderboard();

  document.addEventListener("auth:expired", handleAuthExpired);

  Auth.updateNav();

  if (Auth.isLoggedIn()) {
    goToDashboard();
  } else {
    showView("view-landing");
  }
});
