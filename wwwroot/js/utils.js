/* =====================================================================
   utils.js — storage, toasts, modal, router, small helpers
   ===================================================================== */

const Store = {
  get(key, fallback = null) {
    try {
      const raw = localStorage.getItem(key);
      if (raw === null) return fallback;
      return JSON.parse(raw);
    } catch {
      return fallback;
    }
  },
  set(key, value) {
    try {
      localStorage.setItem(key, JSON.stringify(value));
    } catch {
      /* storage unavailable — fail silently, app still works in-memory */
    }
  },
  remove(key) {
    try { localStorage.removeItem(key); } catch { /* noop */ }
  },
};

/* ---------------- Toasts ---------------- */

function showToast(message, type = "info", timeoutMs = 4200) {
  const container = document.getElementById("toast-container");
  if (!container) return;
  const el = document.createElement("div");
  el.className = `toast ${type}`;
  el.textContent = message;
  container.appendChild(el);
  setTimeout(() => {
    el.style.transition = "opacity .2s ease";
    el.style.opacity = "0";
    setTimeout(() => el.remove(), 200);
  }, timeoutMs);
}

/* ---------------- Confirm modal ---------------- */

function confirmAction({ title, message, confirmLabel = "Confirm" }) {
  return new Promise((resolve) => {
    const overlay = document.getElementById("confirm-modal");
    const titleEl = document.getElementById("confirm-title");
    const msgEl = document.getElementById("confirm-message");
    const okBtn = document.getElementById("confirm-ok");
    const cancelBtn = document.getElementById("confirm-cancel");

    titleEl.textContent = title;
    msgEl.textContent = message;
    okBtn.textContent = confirmLabel;
    overlay.classList.remove("hidden");

    function cleanup(result) {
      overlay.classList.add("hidden");
      okBtn.removeEventListener("click", onOk);
      cancelBtn.removeEventListener("click", onCancel);
      resolve(result);
    }
    function onOk() { cleanup(true); }
    function onCancel() { cleanup(false); }

    okBtn.addEventListener("click", onOk);
    cancelBtn.addEventListener("click", onCancel);
  });
}

/* ---------------- Field error helpers ---------------- */

function setFieldError(elId, message) {
  const el = document.getElementById(elId);
  if (!el) return;
  if (!message) {
    el.classList.add("hidden");
    el.textContent = "";
  } else {
    el.textContent = message;
    el.classList.remove("hidden");
  }
}

/* ---------------- View router ---------------- */

const VIEW_IDS = [
  "view-landing", "view-login", "view-register", "view-forgot",
  "view-dashboard", "view-profile",
  "view-create-room", "view-host-panel",
  "view-join-room", "view-waiting-room",
  "view-test", "view-leaderboard",
];

let isNavigatingHistory = false;

function showView(viewId, pushToHistory = true) {
  VIEW_IDS.forEach((id) => {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.toggle("hidden", id !== viewId);
  });

  // Push state to browser history unless we arrived here via back/forward
  if (pushToHistory && !isNavigatingHistory) {
    history.pushState({ viewId }, "", `#${viewId.replace("view-", "")}`);
  }

  window.scrollTo({ top: 0, behavior: "instant" in window ? "instant" : "auto" });
  document.dispatchEvent(new CustomEvent("viewchange", { detail: { viewId } }));
}

// Listen for browser Back and Forward button clicks
window.addEventListener("popstate", (e) => {
  const targetView = e.state?.viewId;

  // Prevent accidental back-navigation during an active test
  const activeTest = document.getElementById("view-test");
  if (activeTest && !activeTest.classList.contains("hidden")) {
    const confirmLeave = window.confirm("You are currently taking a test. Leaving now will forfeit your progress. Are you sure you want to go back?");
    if (!confirmLeave) {
      history.pushState({ viewId: "view-test" }, "", "#test");
      return;
    }
  }

  if (targetView && VIEW_IDS.includes(targetView)) {
    isNavigatingHistory = true;
    showView(targetView, false);
    isNavigatingHistory = false;
  } else {
    // Fallback if history state is empty
    const fallback = Auth?.isLoggedIn() ? "view-dashboard" : "view-landing";
    isNavigatingHistory = true;
    showView(fallback, false);
    isNavigatingHistory = false;
  }
});

/* ---------------- Misc ---------------- */

function readField(obj, ...candidateNames) {
  if (obj === null || obj === undefined) return undefined;
  if (typeof obj !== "object") return obj; // response was a raw scalar
  for (const name of candidateNames) {
    if (Object.prototype.hasOwnProperty.call(obj, name)) return obj[name];
    const lower = name.charAt(0).toLowerCase() + name.slice(1);
    if (Object.prototype.hasOwnProperty.call(obj, lower)) return obj[lower];
    const allLower = name.toLowerCase();
    const foundKey = Object.keys(obj).find((k) => k.toLowerCase() === allLower);
    if (foundKey) return obj[foundKey];
  }
  return undefined;
}

function pad2(n) { return String(n).padStart(2, "0"); }

function formatElapsed(totalSeconds) {
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = Math.floor(totalSeconds % 60);
  return h > 0 ? `${pad2(h)}:${pad2(m)}:${pad2(s)}` : `${pad2(m)}:${pad2(s)}`;
}

function escapeHtml(str) {
  const div = document.createElement("div");
  div.textContent = str ?? "";
  return div.innerHTML;
}

function setButtonBusy(btn, busy, busyText) {
  if (!btn) return;
  if (busy) {
    btn.dataset.originalText = btn.dataset.originalText || btn.textContent;
    btn.textContent = busyText || "Please wait…";
    btn.disabled = true;
  } else {
    if (btn.dataset.originalText) btn.textContent = btn.dataset.originalText;
    btn.disabled = false;
  }
}

/** 60s cooldown on an OTP "send code" button so it can't be spammed. */
function startOtpCooldown(btn, seconds = 60) {
  let remaining = seconds;
  const original = btn.dataset.originalText || btn.textContent;
  btn.dataset.originalText = original;
  btn.disabled = true;
  btn.textContent = `Resend in ${remaining}s`;
  const timer = setInterval(() => {
    remaining -= 1;
    if (remaining <= 0) {
      clearInterval(timer);
      btn.disabled = false;
      btn.textContent = original;
    } else {
      btn.textContent = `Resend in ${remaining}s`;
    }
  }, 1000);
}
