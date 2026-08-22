using System.Collections.Concurrent;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;

static class App_Funcs
{
    private const byte Password_Length = 8, OTP_Duration_Mins = 5, Max_Otp_Verification_Attempts = 3;
    private static readonly ConcurrentDictionary<string, Active_OTP> Active_otp = new();

    /// <param name="Details">DTO of registration</param>
    /// <param name="Db">Db from asp</param>
    /// <param name="Hash">Hashed secret key</param>
    /// <returns>1st val status and 2nd db id</returns>
    public static async ValueTask<(App_State, int)> User_Registration(Registration_Credentials Details, App_DB_Context Db, byte[] Hash)
    {
        // some checks
        if (Db is null || Details is null || Hash is null)
            return (App_State.Null_Data_Error, -1);

        // min password len must be met
        if (Details.Password.Length < Password_Length) return (App_State.Null_Data_Error, -1);

        // OTP verification is must, and easy so goes 1st
        if (Verify_OTP(Details.Email_id, Details.OTP, Details.Ot_type) != App_State.Op_Success)
            return (App_State.OTP_Verification_Fail, -1);

        bool Completed = false;

        // this one came 1st before encrypt email and pass as if user pre exists
        var Hashed_email_id = Web_Utility.Hash_Email(Details.Email_id, Hash);

        // shall not pre exists
        var User_pre_exists = await Db.Users.FirstOrDefaultAsync(X => X.Hashed_email_id == Hashed_email_id);
        if (User_pre_exists is not null) return (App_State.Email_Already_Exists, -1);

        // repeated checks for erorrs
        var Encrypted_email_id = Web_Utility.Encrypt(Details.Email_id, Hash, out Completed);
        if (!Completed) return (App_State.Null_Data_Error, -1);

        var Hashed_password = Web_Utility.Hash_Password(Details.Password);

        // all info of new user
        var User_data_args = new User_Data_Args()
        {
            DB_id = 0,
            Encrypted_email_id = Encrypted_email_id,
            Hashed_email_id = Hashed_email_id,
            Hashed_password = Hashed_password,
            Name = Details.Name,
        };

        // user created
        var User = new User_Data(User_data_args);

        // added to ram and saved to db
        await Db.Users.AddAsync(User);
        await Db.SaveChangesAsync();

        return (App_State.Op_Success, User.DB_id);
    }

    /// <param name="Hash">Hashed secret key</param>
    public static async ValueTask<(App_State, int)> User_Login(Login_Credentials Details, App_DB_Context Db, byte[] Hash)
    {
        // some checks
        if (Db is null || Details is null || Hash is null)
            return (App_State.Null_Data_Error, -1);

        // hash to match it further
        var Hashed_email_id = Web_Utility.Hash_Email(Details.Email_id, Hash);

        // Must pre exists
        var User = await Db.Users.FirstOrDefaultAsync(X => X.Hashed_email_id == Hashed_email_id);
        if (User is null) return (App_State.User_Not_Exists, -1);

        // verify the hashed password
        if (!Web_Utility.Verify_Hashed_Password(User.Hashed_password, Details.Password)) return (App_State.Wrong_Password, -1);

        return (App_State.Op_Success, User.DB_id);
    }


    public static App_State Add_New_OTP(string Email_id, int OTP, OTP_Type Otp_type)
    {
        // just precaution, valid for 5 mins after generation
        if (!Active_otp.TryAdd(Email_id, new(OTP_Duration_Mins, OTP, Otp_type))) return App_State.OTP_Gen_Failed;

        return App_State.Op_Success;
    }

    public static App_State Verify_OTP(string Email, int OTP, OTP_Type Otp_type)
    {
        if (string.IsNullOrEmpty(Email)) return App_State.OTP_Verification_Fail;

        // if it throws ex
        var Completed = Active_otp.TryGetValue(Email, out var User_otp);
        if (!Completed) return App_State.Null_Data_Error;

        // yay matched
        if (DateTime.UtcNow > User_otp.Finish_time)
        {
            Active_otp.Remove(Email, out User_otp);
        }
        else if (User_otp.OTP == OTP && Otp_type == User_otp.Otp_type)
        {
            Active_otp.Remove(Email, out User_otp);
            return App_State.Op_Success;
        }
        else
        {
            User_otp.Attempt_count++;

            // fail and remove 
            if (User_otp.Attempt_count >= Max_Otp_Verification_Attempts)
                Active_otp.Remove(Email, out User_otp);
        }

        return App_State.OTP_Verification_Fail;
    }

    public static bool OTP_Already_Pending(string Email_id)
    {
        return Active_otp.TryGetValue(Email_id, out var Val);
    }

    /// <summary>
    /// Update pre existing otp
    /// </summary>
    public static bool Update_OTP(string Email_id, int Otp, OTP_Type Otp_type)
    {
        try
        {
            Active_otp.AddOrUpdate(Email_id, new Active_OTP(OTP_Duration_Mins, Otp, Otp_type), (string Key, Active_OTP Val) =>
            {
                Val.OTP = Otp;
                Val.Otp_type = Otp_type;

                return Val;
            });
            return true;
        }
        catch
        {
            return false;
        }

    }


    /// <summary>
    /// Only checks if data exists and is not null, true if passes
    /// </summary>
    public static bool Verify_Registration_Data(Registration_Credentials Data)
    {
        if (Data is null || string.IsNullOrEmpty(Data.Email_id) || string.IsNullOrEmpty(Data.Password) ||
        string.IsNullOrEmpty(Data.Name) || !MailAddress.TryCreate(Data.Email_id, out var Addr))
            return false;

        return true;
    }

    /// <summary>
    /// Only checks if data exists and is not null, true if passes
    /// </summary>
    public static bool Verify_Login_Data(Login_Credentials Data)
    {
        if (Data is null || string.IsNullOrEmpty(Data.Email_id) || string.IsNullOrEmpty(Data.Password)
        || !MailAddress.TryCreate(Data.Email_id, out var Addr))
            return false;

        return true;
    }


    public static async ValueTask<App_State> Update_Password(Password_Update_Data Data, byte[] Hashed_key, App_DB_Context Db)
    {
        // veirfy otp
        var Result_state = Verify_OTP(Data.Email_id, Data.OTP, Data.Ot_type);
        if (Result_state != App_State.Op_Success)
            return Result_state;

        // hash the id
        var Hashed_email_id = Web_Utility.Hash_Email(Data.Email_id, Hashed_key);

        // get id
        var User = await Db.Users.FirstOrDefaultAsync(X => X.Hashed_email_id == Hashed_email_id);
        if (User is null) return App_State.User_Not_Exists;

        // update user password
        User.Hashed_password = Web_Utility.Hash_Password(Data.Password);
        Db.Users.Update(User);
        await Db.SaveChangesAsync();

        return App_State.Op_Success;
    }


    public static IResult Create_Room(int Db_id, Room_Creation_Info Info)
    {
        // must
        if (!Verify_Test_Paper(Info.Test_paper)) return Get_Error_Msg(App_State.Test_Wrong_Format);

        // crefully make to prevent race conditions
        var Room_id = Test_Room_Manager.Create_Room(Db_id, Info, out var Success);
        if (!Success) return Get_Error_Msg(App_State.Room_Creation_Failed);

        return Results.Ok(Room_id);
    }

    public static App_State Start_Room(int Db_id, Room_Joining_Info Info)
    {
        // get room if user has created it
        var Room_info = Test_Room_Manager.Get_Room(Db_id);
        if (Room_info.Item1 != App_State.Op_Success) return Room_info.Item1;

        // check password / not needed btw it is owned by that person
        //if (Room_info.Item2!.Password != Info.Password) return App_State.Room_Password_Wrong;

        return Room_info.Item2!.Start();
    }


    public static App_State Join_Room(int Db_id, Room_Joining_Info Info)
    {
        // room
        var Room_state = Test_Room_Manager.Get_Room_Outsider(Info.Room_id);
        if (Room_state.Item1 != App_State.Op_Success) return Room_state.Item1;

        // check password
        if (Room_state.Item2!.Password != Info.Password) return App_State.Room_Password_Wrong;

        // try add to room
        var State = Room_state.Item2.Add_Test_Taker(Db_id, Info.Name);

        return State; ;
    }

    public static App_State Leave_Room(int Db_id, Room_Joining_Info Info)
    {
        // room, outsider func is for finding by room id
        var Room_state = Test_Room_Manager.Get_Room_Outsider(Info.Room_id);
        if (Room_state.Item1 != App_State.Op_Success) return Room_state.Item1;

        // check password
        if (Room_state.Item2!.Password != Info.Password) return App_State.Room_Password_Wrong;

        // try add to room
        var State = Room_state.Item2.Remove_Test_Taker(Db_id);

        return State; ;
    }


    public static App_State Upload_Test_Json(int Db_id, Test_Paper Test)
    {
        if (!Verify_Test_Paper(Test)) return App_State.Test_Wrong_Format;

        var Room = Test_Room_Manager.Get_Room(Db_id);
        if (Room.Item1 != App_State.Op_Success) return Room.Item1;

        // set test
        return Room.Item2!.Update_Test(Test);
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

        // set the que adn ans
        Ques = new(Room.Item2.Test_paper.Questions, Room.Item2.Test_paper.Options);

        return App_State.Op_Success;
    }
    
    public static App_State Sumbit_Test(int Db_id, Test_Ans Ans, out int Score)
    {
        Score = 0;

        // room
        var Room_info = Test_Room_Manager.Get_Room_Outsider(Ans.Room_id);
        if (Room_info.Item1 != App_State.Op_Success) return Room_info.Item1;

        // passi passi
        if (Ans.Password != Room_info.Item2!.Password) return App_State.Room_Password_Wrong;

        // maki result
        Room_info.Item2!.Evaluate(Db_id, Ans.Answers, out var User_score);
        Score = User_score;

        return App_State.Op_Success;
    }

    public static App_State Get_Leaderboard(int Db_id, Room_Joining_Info Info, out Test_Leaderboard Leaderboard)
    {
        Leaderboard = null!;

        // roomi
        var Room_info = Test_Room_Manager.Get_Room_Outsider(Info.Room_id);
        if (Room_info.Item1 != App_State.Op_Success) return Room_info.Item1;

        // poki
        if (Info.Password != Room_info.Item2!.Password) return App_State.Room_Password_Wrong;

        // bop
        Leaderboard = Room_info.Item2.Leaderboard;

        return App_State.Op_Success;
    }

    public static async ValueTask<(App_State, string)> Get_Name(int Db_id, App_DB_Context Db)
    {
        var User = await Db.Users.FindAsync(Db_id);
        if (User is null) return (App_State.User_Not_Exists, "Invalid");

        return (App_State.Op_Success, User.Name);
    }

    // gemini made for detailed erroe reponse
    public static IResult Get_Error_Msg(App_State state)
    {
        var (statusCode, title, detail) = state switch
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

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
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
