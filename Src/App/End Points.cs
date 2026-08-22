
// insert db id in jwt tokken for various use cases
static class End_Points
{
    // Base site names
    public const string Base = "TestArena/",
    Base_Test_Room = "TestRoom/",
    Base_User = "User/",

    Register_User = Base + "RegisterNewUser", // POST, send dto for registration
    Login_User = Base + "LoginUser", // POST, send dto for login

    Send_OTP = Base + "SendOTP", // POST, send dto OTP_Req
    Forgot_Password = Base + "ForgotPassword", // POST, identical to regis ie use otp

    Create_Room = Base + Base_Test_Room + "CreateRoom", // POST room creation info dto
    Start_Room = Base + Base_Test_Room + "StartRoom", // PATCH ?, send room joining dto, it will match db id to start

    Upload_Test_Json = Base + Base_Test_Room + "UploadTest", // POST, send dto to this 

    Join_Room = Base + Base_User + "JoinRoom", // POST, send room join info dto
    Leave_Room = Base + Base_User + "LeaveRoom", // PATCH/POST, send room joining info dto

    Get_Questions = Base + Base_Test_Room + "GetQues", // POST
    Sumbit_Test = Base + Base_Test_Room + "SubmitTest", // POST, dto of ans
    Get_LeaderBoard = Base + Base_Test_Room + "GetLeaderboard", // POST ?, send room join dto for verification

    Get_Name = Base + Base_User + "GetName", // GET, verify using token
    Sign_Out = Base + "SignOut";
}
