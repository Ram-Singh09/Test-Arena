/* =====================================================================
   config.js
   Endpoint paths are copied verbatim from the C# End_Points class,
   including its exact casing, so nothing here should need editing
   unless the backend's own paths change.
   ===================================================================== */

const CONFIG = (() => {
  const Base = "TestArena/";
  const Base_Test_Room = Base + "TestRoom/";
  const Base_User = Base + "User/";

  const ENDPOINTS = {
    Register_User: { path: Base + "RegisterNewUser", method: "POST" },
    Login_User: { path: Base + "LoginUser", method: "POST" },

    Send_OTP: { path: Base + "SendOTP", method: "POST" },
    Forgot_Password: { path: Base + "ForgotPassword", method: "POST" },

    Create_Room: { path: Base_Test_Room + "CreateRoom", method: "POST" },
    Start_Room: { path: Base_Test_Room + "StartRoom", method: "POST" },

    Upload_Test_Json: { path: Base_Test_Room + "UploadTest", method: "PATCH" },

    Join_Room: { path: Base_User + "JoinRoom", method: "POST" },
    Leave_Room: { path: Base_User + "LeaveRoom", method: "PATCH" },

    Get_Questions: { path: Base_Test_Room + "GetQues", method: "POST" },
    Sumbit_Test: { path: Base_Test_Room + "SubmitTest", method: "POST" },
    Get_LeaderBoard: { path: Base_Test_Room + "GetLeaderboard", method: "POST" },

    Get_Name: { path: Base_User + "GetName", method: "GET" },
    // No HTTP method was given for Sign_Out in the backend mapping —
    // POST is assumed. If your backend uses a different verb, change it here.
    Sign_Out: { path: Base + "SignOut", method: "POST" },
  };

  // OTP_Type enum — byte, values in declaration order.
  const OTP_TYPE = {
    Null: 0,
    Registration: 1,
    Password_Update: 2,
  };

  const STORAGE_KEYS = {
    API_BASE_URL: "ta_api_base_url",
    THEME: "ta_theme",
    TOKEN: "ta_token",
    USER: "ta_user", // { name, email }
    ROOM_SESSION: "ta_room_session", // participant's current room join info
    SUBMITTED_ROOMS: "ta_submitted_rooms", // array of room ids already submitted, this browser
  };

  return { ENDPOINTS, OTP_TYPE, STORAGE_KEYS };
})();
