/* =====================================================================
   auth.js — register / login / forgot password / sign out
   ===================================================================== */

function decodeJwtPayload(token) {
  try {
    const part = token.split(".")[1];
    const base64 = part.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");
    const json = decodeURIComponent(
      atob(padded).split("").map((c) => "%" + c.charCodeAt(0).toString(16).padStart(2, "0")).join("")
    );
    return JSON.parse(json);
  } catch {
    return null;
  }
}

const Auth = (() => {
  function getUser() {
    return Store.get(CONFIG.STORAGE_KEYS.USER, null);
  }

  function setUser(user) {
    Store.set(CONFIG.STORAGE_KEYS.USER, user);
    updateNav();
  }

  function isLoggedIn() {
    return !!Api.getToken();
  }

  function updateNav() {
    const authed = document.getElementById("nav-authed");
    const guest = document.getElementById("nav-guest");
    const nameEl = document.getElementById("nav-user-name");
    if (isLoggedIn()) {
      authed.classList.remove("hidden");
      guest.classList.add("hidden");
      const user = getUser();
      nameEl.textContent = user?.name || user?.email || "";
    } else {
      authed.classList.add("hidden");
      guest.classList.remove("hidden");
    }
  }

  function userFromLoginResponse(token, typedEmail) {
    const claims = decodeJwtPayload(token) || {};
    const normalizedEmail = (typedEmail || "").trim().toLowerCase();
    const storedName = Store.get("ta_name_" + normalizedEmail, null);

    // Prioritize stored name from registration -> JWT claim -> default to "User"
    const name =
      storedName ||
      readField(claims, "name", "unique_name", "given_name") ||
      claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] ||
      "User";

    const email =
      readField(claims, "email") ||
      claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"] ||
      typedEmail || "";

    return { name, email };
  }

  async function login(email, password) {
    const data = await Api.loginUser({ email, password });
    const token = readField(data, "Token", "token");
    if (!token || typeof token !== "string") {
      throw new ApiError("Logged in, but no token was returned by the server.", 0);
    }
    Api.setToken(token);
    setUser(userFromLoginResponse(token, email));
  }

  async function logout() {
    try {
      if (isLoggedIn()) await Api.signOut();
    } catch {
      /* best-effort */
    }
    Api.setToken(null);
    Store.remove(CONFIG.STORAGE_KEYS.USER);
    Store.remove(CONFIG.STORAGE_KEYS.ROOM_SESSION);
    updateNav();
  }

  return { getUser, setUser, isLoggedIn, updateNav, login, logout };
})();

/* ---------------- Wiring ---------------- */

function wireAuthForms() {
  document.getElementById("nav-login-btn").addEventListener("click", () => showView("view-login"));
  document.getElementById("nav-register-btn").addEventListener("click", () => showView("view-register"));
  document.getElementById("hero-login-btn").addEventListener("click", () => showView("view-login"));
  document.getElementById("hero-register-btn").addEventListener("click", () => showView("view-register"));
  document.getElementById("goto-forgot-btn").addEventListener("click", () => showView("view-forgot"));
  document.getElementById("goto-register-btn").addEventListener("click", () => showView("view-register"));
  document.getElementById("register-goto-login-btn").addEventListener("click", () => showView("view-login"));
  document.getElementById("forgot-goto-login-btn").addEventListener("click", () => showView("view-login"));
  document.getElementById("nav-signout-btn").addEventListener("click", async () => {
    await Auth.logout();
    showToast("Signed out.", "info");
    showView("view-landing");
  });

  /* ---- Login ---- */
  document.getElementById("login-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    setFieldError("login-error", "");
    const email = document.getElementById("login-email").value.trim();
    const password = document.getElementById("login-password").value;
    const btn = document.getElementById("login-submit-btn");
    setButtonBusy(btn, true, "Logging in…");
    try {
      await Auth.login(email, password);
      showToast(`Welcome back, ${Auth.getUser()?.name || ""}.`, "success");
      goToDashboard();
    } catch (err) {
      setFieldError("login-error", err.message || "Couldn't log in.");
    } finally {
      setButtonBusy(btn, false);
    }
  });

  /* ---- Register ---- */
  document.getElementById("register-send-otp-btn").addEventListener("click", async () => {
    setFieldError("register-error", "");
    const email = document.getElementById("register-email").value.trim();
    const name = document.getElementById("register-name").value.trim();
    if (!email || !name) {
      setFieldError("register-error", "Enter your name and email first, then send the code.");
      return;
    }
    const btn = document.getElementById("register-send-otp-btn");
    try {
      await Api.sendOtp({ email, name, otpType: CONFIG.OTP_TYPE.Registration });
      showToast("Verification code sent — check your email.", "success");
      startOtpCooldown(btn);
    } catch (err) {
      setFieldError("register-error", err.message || "Couldn't send the code.");
    }
  });

  document.getElementById("register-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    setFieldError("register-error", "");
    const name = document.getElementById("register-name").value.trim();
    const email = document.getElementById("register-email").value.trim();
    const password = document.getElementById("register-password").value;
    const confirm = document.getElementById("register-confirm").value;
    const otp = document.getElementById("register-otp").value.trim();

    if (password !== confirm) {
      setFieldError("register-error", "Passwords don't match.");
      return;
    }
    if (!otp) {
      setFieldError("register-error", "Enter the verification code sent to your email.");
      return;
    }
    const btn = document.getElementById("register-submit-btn");
    setButtonBusy(btn, true, "Creating account…");
    try {
      await Api.registerUser({
        email, password, name,
        otp: parseInt(otp, 10),
        otpType: CONFIG.OTP_TYPE.Registration,
      });

      // Save real display name keyed by email
      Store.set("ta_name_" + email.toLowerCase(), name);

      showToast("Account created — log in to continue.", "success");
      document.getElementById("login-email").value = email;
      document.getElementById("register-form").reset();
      showView("view-login");
    } catch (err) {
      setFieldError("register-error", err.message || "Couldn't create the account.");
    } finally {
      setButtonBusy(btn, false);
    }
  });

  /* ---- Forgot password ---- */
  document.getElementById("forgot-send-otp-btn").addEventListener("click", async () => {
    setFieldError("forgot-error", "");
    const email = document.getElementById("forgot-email").value.trim();
    const name = document.getElementById("forgot-name").value.trim();
    if (!email || !name) {
      setFieldError("forgot-error", "Enter your name and email first, then send the code.");
      return;
    }
    const btn = document.getElementById("forgot-send-otp-btn");
    try {
      await Api.sendOtp({ email, name, otpType: CONFIG.OTP_TYPE.Password_Update });
      showToast("Verification code sent — check your email.", "success");
      startOtpCooldown(btn);
    } catch (err) {
      setFieldError("forgot-error", err.message || "Couldn't send the code.");
    }
  });

  document.getElementById("forgot-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    setFieldError("forgot-error", "");
    const email = document.getElementById("forgot-email").value.trim();
    const otp = document.getElementById("forgot-otp").value.trim();
    const password = document.getElementById("forgot-password").value;
    const confirm = document.getElementById("forgot-confirm").value;

    if (password !== confirm) {
      setFieldError("forgot-error", "Passwords don't match.");
      return;
    }
    if (!otp) {
      setFieldError("forgot-error", "Enter the verification code sent to your email.");
      return;
    }
    const btn = document.getElementById("forgot-submit-btn");
    setButtonBusy(btn, true, "Resetting…");
    try {
      await Api.forgotPassword({
        email, password,
        otp: parseInt(otp, 10),
        otpType: CONFIG.OTP_TYPE.Password_Update,
      });
      showToast("Password updated — log in with your new password.", "success");
      document.getElementById("login-email").value = email;
      document.getElementById("forgot-form").reset();
      showView("view-login");
    } catch (err) {
      setFieldError("forgot-error", err.message || "Couldn't reset the password.");
    } finally {
      setButtonBusy(btn, false);
    }
  });
}

function renderDashboardHostedRoom() {
  const hosted = Store.get("ta_hosted_room", null);
  const card = document.getElementById("dash-hosted-card");
  
  if (!card) return;

  if (hosted && hosted.roomId) {
    document.getElementById("dash-hosted-id").textContent = String(hosted.roomId);
    document.getElementById("dash-hosted-pass").textContent = String(hosted.password);
    document.getElementById("dash-hosted-status").textContent = hosted.started ? "Live / In Progress" : "Waiting to Start";
    card.classList.remove("hidden");

    // All click handlers safely inside the check
    document.getElementById("dash-hosted-take-btn").onclick = () => {
      enterWaitingRoom({
        roomId: hosted.roomId,
        password: hosted.password,
        name: hosted.creatorName,
      });
    };

    document.getElementById("dash-hosted-manage-btn").onclick = () => {
      HostPanel.openWithRoom(hosted);
    };

    document.getElementById("dash-hosted-lb-btn").onclick = () => {
      Leaderboard.open({
        roomId: hosted.roomId,
        password: hosted.password,
        name: hosted.creatorName,
        viewerName: hosted.creatorName,
      });
    };

    // The new dismiss/clear button for stale rooms
    document.getElementById("dash-hosted-clear-btn").onclick = async () => {
      const ok = await confirmAction({
        title: "Dismiss Room?",
        message: "This removes the room from your dashboard. Only do this if the test is over or the server deleted it.",
        confirmLabel: "Dismiss",
      });
      if (ok) {
        Store.remove("ta_hosted_room");
        renderDashboardHostedRoom(); // Re-render to hide the card immediately
      }
    };
  } else {
    card.classList.add("hidden");
  }
}

function goToDashboard() {
  const user = Auth.getUser();
  document.getElementById("dashboard-welcome").textContent = user?.name ? `Welcome, ${user.name}` : "Welcome";
  renderDashboardHostedRoom();
  showView("view-dashboard");
}