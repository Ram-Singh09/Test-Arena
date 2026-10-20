
static class App_Funcs
{
    public static IResult Create_Room(User_ID User_id, Room_Creation_Info Info)
    {
        if (User_id is null) return Results.Problem("User id provided is null");

        // must
        if (!Verify_Test_Paper(Info.Test_paper)) return Get_Error_Msg(App_State.Test_Wrong_Format);

        // crefully make to prevent race conditions
        var Room_id = Test_Room_Manager.Create_Room(User_id.Id, Info, out var Success);
        if (!Success) return Get_Error_Msg(App_State.Room_Creation_Failed);

        return Results.Ok(Room_id);
    }

    public static App_State Join_Room(uint User_id, Room_Joining_Info Info)
    {
        // room
        var Room_state = Test_Room_Manager.Get_Room(Info.Room_id);
        if (Room_state.Item1 != App_State.Op_Success) return Room_state.Item1;

        // check password
        if (Room_state.Item2!.Password != Info.Password) return App_State.Room_Password_Wrong;

        // try add to room
        var State = Room_state.Item2.Add_Test_Taker(User_id, Info.Name);

        return State; ;
    }

    public static App_State Leave_Room(uint User_id, Room_Joining_Info Info)
    {
        // room, outsider func is for finding by room id
        var Room_state = Test_Room_Manager.Get_Room(Info.Room_id);
        if (Room_state.Item1 != App_State.Op_Success) return Room_state.Item1;

        // check password
        if (Room_state.Item2!.Password != Info.Password) return App_State.Room_Password_Wrong;

        // try add to room
        var State = Room_state.Item2.Remove_Test_Taker(User_id);

        return State; ;
    }

    /// <returns>True if all things are right else false</returns>
    public static bool Verify_Test_Paper(Test_Paper Info)
    {
        return Info is not null && Info.Ans is not null && Info.Questions is not null
        && Info.Options is not null && Info.Questions.Length != 0 &&
        Info.Questions.Length == Info.Ans.Length && Info.Options.Length == Info.Questions.Length * Test_Paper.Options_Per_Que;
    }

    public static App_State Get_Question_Paper(Room_Joining_Info Info, out Questions_Set Ques)
    {
        Ques = null!;

        // skipped checking if req is from someone who is part of room, too much work for cpu to do small thing
        var Room = Test_Room_Manager.Get_Room(Info. Room_id);
        if (Room.Item1 != App_State.Op_Success) return Room.Item1;

        if (Info.Password != Room.Item2!.Password)
            return App_State.Room_Password_Wrong;
        
        if (!Room.Item2.Test_takers.ContainsKey(Info.User_id.Id))
            return App_State.Not_Part_Of_Room;

        // set the que adn ans
        Ques = new(Room.Item2.Test_paper.Questions, Room.Item2.Test_paper.Options);
        
        return App_State.Op_Success;
    }
    
    public static App_State Sumbit_Test(uint User_id, Test_Ans Ans, out int Score)
    {
        Score = 0;

        // room
        var Room_info = Test_Room_Manager.Get_Room(Ans.Room_id);
        if (Room_info.Item1 != App_State.Op_Success) return Room_info.Item1;

        // passi passi
        if (Ans.Password != Room_info.Item2!.Password) return App_State.Room_Password_Wrong;

        // maki result
        Room_info.Item2!.Evaluate(User_id, Ans.Answers, out var User_score);
        Score = User_score;

        return App_State.Op_Success;
    }

    public static App_State Get_Leaderboard(uint User_id, Room_Joining_Info Info, out Test_Leaderboard Leaderboard)
    {
        Leaderboard = null!;

        // roomi
        var Room_info = Test_Room_Manager.Get_Room(Info.Room_id);
        if (Room_info.Item1 != App_State.Op_Success) return Room_info.Item1;

        // poki
        if (Info.Password != Room_info.Item2!.Password) return App_State.Room_Password_Wrong;

        // bop
        Leaderboard = Room_info.Item2.Leaderboard;

        return App_State.Op_Success;
    }

    // gemini made for detailed erroe reponse
    public static IResult Get_Error_Msg(App_State state)
    {
        var (Status_code, Title, Detail) = state switch
        {
            // 400 Bad Request - Client validation & state violations
            App_State.Password_Too_Short => (400, "Invalid Password", "Password does not meet minimum length requirements."),
            App_State.Test_Wrong_Format => (400, "Invalid Test Paper", "The test paper structure or option count is invalid."),
            App_State.Null_Data_Error => (400, "Missing Payload", "Request body or required fields cannot be null."),
            App_State.Insufficient_Data => (400, "Incomplete Data", "Required parameters are missing."),
            App_State.Room_Not_Started => (400, "Room Not Started", "The test room has not been started yet."),
            App_State.Test_Time_Expired => (400, "Time Expired", "The test submission deadline has passed."),
            App_State.Test_Already_Submited => (400, "Already Submitted", "You have already submitted answers for this test."),

            // 401 Unauthorized - Authentication & credential failures
            App_State.Wrong_Credentials => (401, "Auth Failed", "Invalid email or password."),
            App_State.Wrong_Password => (401, "Auth Failed", "Incorrect password."),
            App_State.Token_Data_Null => (401, "Unauthorized", "JWT claims or token identification missing."),
            App_State.OTP_Verification_Fail => (401, "OTP Failed", "Invalid or expired OTP code."),
            App_State.Room_Password_Wrong => (401, "Room Auth Failed", "Incorrect room password provided."),

            // 403 Forbidden - Ownership & permission issues
            App_State.Not_Owns_Room => (403, "Forbidden", "You are not the creator/host of this room."),
            App_State.Not_Part_Of_Room => (403, "Forbidden", "You are not an active participant in this room."),

            // 404 Not Found - Missing entities
            App_State.User_Not_Exists => (404, "User Not Found", "The requested user account does not exist."),
            App_State.Room_Not_Exists => (404, "Room Not Found", "The specified test room does not exist or has expired."),
            App_State.Test_Taker_Not_Found => (404, "Participant Not Found", "No participant matching this identity exists in the room."),
            App_State.Que_Not_Exists => (404, "Question Not Found", "The requested question ID was not found."),
            App_State.Ans_Not_Exists => (404, "Option Not Found", "The selected answer option index does not exist."),
            App_State.No_Test_Subited => (404, "No Submissions", "No participants submitted answers for this room."),

            // 409 Conflict - State collisions & duplicate actions
            App_State.Email_Already_Exists => (409, "Email Taken", "An account with this email address already exists."),
            App_State.Already_Part_Of_Room => (409, "Already Joined", "You are already joined to this room."),
            App_State.Room_Already_Started => (409, "Room In Progress", "Cannot join or modify a room that has already started."),
            App_State.Room_Closed => (409, "Room Closed", "This test room is closed and no longer accepting requests."),
            App_State.Test_Completed => (409, "Test Finished", "This test session has concluded."),
            App_State.OTP_Already_Pending => (409, "OTP Active", "An active OTP is already in-flight. Please wait before retrying."),

            // 500 Internal Server Error - Execution / Database / Generation failures
            App_State.Registration_Failed => (500, "Registration Error", "Failed to create user account in database."),
            App_State.Login_Failed => (500, "Login Error", "Failed to process login request."),
            App_State.Room_Creation_Failed => (500, "Room Creation Error", "Failed to allocate room resources."),
            App_State.Join_Room_Failed => (500, "Join Error", "Internal error occurred while adding user to room."),
            App_State.Leave_Room_Failed => (500, "Leave Error", "Internal error occurred while removing user from room."),
            App_State.OTP_Gen_Failed => (500, "OTP Error", "Failed to generate or dispatch OTP."),
            App_State.Test_Submission_Failed => (500, "Submission Error", "Failed to record test answers."),
            App_State.Result_Gen_Failed => (500, "Scoring Error", "Failed to calculate final test results."),

            // Default / Success fallback
            _ => (500, "Server Error", "An unexpected error occurred.")
        };

        Console.WriteLine(Title + Detail);

        return Results.Problem(
            statusCode: Status_code,
            title: Title,
            detail: Detail,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = state.ToString()
            }
        );
    }

}

enum App_State : byte
{
    Op_Success, // universal, tell no eeor occured everything done for req

    Email_Already_Exists, Password_Too_Short, // data issue
    Wrong_Credentials, Wrong_Password, // verification issue

    Registration_Failed, Login_Failed, // unknown prob occured

    Room_Creation_Failed, Room_Closed, // undefined failure

    Not_Owns_Room, Room_Not_Exists, Room_Password_Wrong, // ownership and verification issue
    Already_Part_Of_Room, Not_Part_Of_Room,

    Join_Room_Failed, Leave_Room_Failed, // undefined failure
    Room_Already_Started, Room_Not_Started,
    Test_Completed,

    Test_Wrong_Format, Test_Taker_Not_Found,

    OTP_Already_Pending, OTP_Gen_Failed,
    OTP_Verification_Fail, // otp related

    Que_Not_Exists, Ans_Not_Exists,
    Test_Time_Expired,
    Test_Already_Submited, Test_Submission_Failed,
                        // no one submited test so no scores
    Result_Gen_Failed, No_Test_Subited,

    Token_Data_Null,

    Null_Data_Error, Insufficient_Data,
    User_Not_Exists,
}
