
// insert db id in jwt tokken for various use cases
static class End_Points
{
    // Base site names
    public const string Base = "/TestArena/",
    Api_Paths = "/ApiPaths",

    Create_Room = Base + "CreateRoom", // POST room creation info dto
    Get_User_Id = Base + "GetUserId", // GET

    Join_Room = Base + "JoinRoom", // POST, send room join info dto
    Leave_Room = Base + "LeaveRoom", // PATCH/POST, send room joining info dto

    Get_Questions = Base + "GetQues", // POST
    Sumbit_Test = Base + "SubmitTest", // POST, dto of ans
    Get_LeaderBoard = Base + "GetLeaderboard"; // POST ?, send room join dto for verification
}
