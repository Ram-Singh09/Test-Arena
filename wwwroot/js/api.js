/* =====================================================================
   api.js
   Thin wrapper around fetch(). Request bodies use the exact property
   names/casing declared on the C# DTOs (including the backend's own
   "Positve_marks" and "Ot_type" / "Otp_type" spellings) so this works
   whether or not the server's JSON options are case-insensitive.
   Response bodies are read defensively via readField(), since we don't
   know the server's output casing convention.
   ===================================================================== */

class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

const Api = (() => {
  function getBaseUrl() {
    return "/";
  }

  function setBaseUrl(url) {
    
  }

  function isConfigured() {
    return true;
  }

  function getToken() {
    return Store.get(CONFIG.STORAGE_KEYS.TOKEN, null);
  }

  function setToken(token) {
    if (token) Store.set(CONFIG.STORAGE_KEYS.TOKEN, token);
    else Store.remove(CONFIG.STORAGE_KEYS.TOKEN);
  }

  async function request(endpointKey, { body = undefined, auth = false } = {}) {
    const endpoint = CONFIG.ENDPOINTS[endpointKey];
    if (!endpoint) throw new ApiError(`Unknown endpoint "${endpointKey}"`, 0);

    const base = getBaseUrl();
    if (!base) throw new ApiError("Backend URL is not configured yet.", 0);

    const url = base + endpoint.path;
    const headers = { "Content-Type": "application/json" };
    if (auth) {
      const token = getToken();
      if (!token) throw new ApiError("You're signed out — please log in again.", 401);
      headers.Authorization = `Bearer ${token}`;
    }

    let res;
    try {
      res = await fetch(url, {
        method: endpoint.method,
        headers,
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });
    } catch (networkErr) {
      throw new ApiError(
        `Couldn't reach the backend at ${base} — check the URL, that the server is running, and CORS settings.`,
        0
      );
    }

    const text = await res.text();
    let data = null;
    if (text) {
      try { data = JSON.parse(text); } catch { data = text; }
    }

    if (!res.ok) {
      const serverMessage =
        (data && typeof data === "object" && (data.message || data.title || data.error || data.detail)) ||
        (typeof data === "string" ? data : null);
      if (res.status === 401) {
        setToken(null);
        document.dispatchEvent(new CustomEvent("auth:expired"));
        throw new ApiError(serverMessage || "Your session has expired — please log in again.", 401);
      }
      throw new ApiError(serverMessage || `Request failed (${res.status} ${res.statusText})`, res.status);
    }

    return data;
  }

  /* ---------------- Auth ---------------- */

  function sendOtp({ email, name, otpType }) {
    return request("Send_OTP", {
      body: { Email_id: email, Name: name, Otp_type: otpType },
    });
  }

  function registerUser({ email, password, name, otp, otpType }) {
    return request("Register_User", {
      body: { Email_id: email, Password: password, Name: name, OTP: otp, Ot_type: otpType },
    });
  }

  function loginUser({ email, password }) {
    return request("Login_User", {
      body: { Email_id: email, Password: password },
    });
  }

  function forgotPassword({ email, password, otp, otpType }) {
    return request("Forgot_Password", {
      body: { Email_id: email, Password: password, OTP: otp, Ot_type: otpType },
    });
  }

  function signOut() {
    return request("Sign_Out", { auth: true });
  }

  function getName() {
    return request("Get_Name", { auth: true });
  }

  /* ---------------- Rooms (host) ---------------- */

  function createRoom({ minutes, password, positiveMarks, negativeMarks, creatorName, testPaper }) {
    return request("Create_Room", {
      auth: true,
      body: {
        Minutes: minutes,
        Password: password,
        Positve_marks: positiveMarks, // matches backend's own spelling
        Negative_marks: negativeMarks,
        Creator_name: creatorName,
        Test_paper: {
          Questions: testPaper.questions,
          Options: testPaper.options,
          Ans: testPaper.ans,
        },
      },
    });
  }

  function startRoom({ roomId, password, name }) {
    return request("Start_Room", {
      auth: true,
      body: { Room_id: roomId, Password: password, Name: name },
    });
  }

  function uploadTest({ questions, options, ans }) {
    return request("Upload_Test_Json", {
      auth: true,
      body: { Questions: questions, Options: options, Ans: ans },
    });
  }

  /* ---------------- Rooms (participant) ---------------- */

  function joinRoom({ roomId, password, name }) {
    return request("Join_Room", {
      auth: true,
      body: { Room_id: roomId, Password: password, Name: name },
    });
  }

  function leaveRoom({ roomId, password, name }) {
    return request("Leave_Room", {
      auth: true,
      body: { Room_id: roomId, Password: password, Name: name },
    });
  }

  function getQuestions({ roomId, password, name }) {
    return request("Get_Questions", {
      auth: true,
      body: { Room_id: roomId, Password: password, Name: name },
    });
  }

  function submitTest({ answers, roomId, password }) {
    return request("Sumbit_Test", {
      auth: true,
      body: { Answers: answers, Room_id: roomId, Password: password },
    });
  }

  function getLeaderboard({ roomId, password, name }) {
    return request("Get_LeaderBoard", {
      auth: true,
      body: { Room_id: roomId, Password: password, Name: name },
    });
  }

  return {
    getBaseUrl, setBaseUrl, isConfigured,
    getToken, setToken,
    sendOtp, registerUser, loginUser, forgotPassword, signOut,
    createRoom, startRoom, uploadTest, getName,
    joinRoom, leaveRoom, getQuestions, submitTest, getLeaderboard,
  };
})();

/* ---------------- API base URL config modal ---------------- */

const ApiConfigModal = {
  init() {
    this.open = () => {}; // Do nothing
  }
};
