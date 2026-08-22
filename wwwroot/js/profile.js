/* =====================================================================
   profile.js — account details + change password
   ===================================================================== */

function wireProfile() {
  document.getElementById("nav-dashboard-btn").addEventListener("click", goToDashboard);
  document.getElementById("nav-profile-btn").addEventListener("click", openProfile);
  document.getElementById("dash-create-room-btn").addEventListener("click", () => openCreateRoom());
  document.getElementById("dash-join-room-btn").addEventListener("click", () => showView("view-join-room"));
  document.getElementById("brand-home-btn").addEventListener("click", () => {
    showView(Auth.isLoggedIn() ? "view-dashboard" : "view-landing");
    if (Auth.isLoggedIn()) goToDashboard();
  });

  document.getElementById("profile-send-otp-btn").addEventListener("click", async () => {
    setFieldError("profile-password-error", "");
    const user = Auth.getUser();
    if (!user?.email) {
      setFieldError("profile-password-error", "We don't have your email on file — log out and back in.");
      return;
    }
    const btn = document.getElementById("profile-send-otp-btn");
    try {
      await Api.sendOtp({ email: user.email, name: user.name || user.email, otpType: CONFIG.OTP_TYPE.Password_Update });
      showToast("Verification code sent — check your email.", "success");
      startOtpCooldown(btn);
    } catch (err) {
      setFieldError("profile-password-error", err.message || "Couldn't send the code.");
    }
  });

  document.getElementById("change-password-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    setFieldError("profile-password-error", "");
    const user = Auth.getUser();
    const otp = document.getElementById("profile-otp").value.trim();
    const password = document.getElementById("profile-new-password").value;
    const confirm = document.getElementById("profile-confirm-password").value;

    if (password !== confirm) {
      setFieldError("profile-password-error", "Passwords don't match.");
      return;
    }
    if (!otp) {
      setFieldError("profile-password-error", "Enter the verification code sent to your email.");
      return;
    }
    const btn = document.getElementById("profile-password-submit-btn");
    setButtonBusy(btn, true, "Updating…");
    try {
      await Api.forgotPassword({
        email: user.email, password,
        otp: parseInt(otp, 10),
        otpType: CONFIG.OTP_TYPE.Password_Update,
      });
      showToast("Password updated.", "success");
      document.getElementById("change-password-form").reset();
    } catch (err) {
      setFieldError("profile-password-error", err.message || "Couldn't update the password.");
    } finally {
      setButtonBusy(btn, false);
    }
  });
}

function openProfile() {
  const user = Auth.getUser();
  document.getElementById("profile-name-display").value = user?.name || "User";
  document.getElementById("profile-email-display").value = user?.email || "";
  showView("view-profile");
}
