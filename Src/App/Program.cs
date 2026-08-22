using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string DB_File_Path = "DB\\App Data.db";
const string Master_Key_Name = "MasterKey";

// create db directory to ensure future actions
if (!Directory.Exists("DB")) Directory.CreateDirectory("DB");
if (!File.Exists(DB_File_Path)) File.Create(DB_File_Path).Close();

// creates web app to allow changes in final web app
var Web_builder = WebApplication.CreateBuilder(args);

// secret and its bytes conversion to SHA256 by web utility
if (string.IsNullOrEmpty(Web_builder.Configuration[Master_Key_Name])) throw new NullReferenceException("App - Issue in obtaining Key from config");
byte[] Secret_hash = Web_Utility.Key_To_Hashed_Bytes(Web_builder.Configuration[Master_Key_Name]!, out bool Key_created);
if (!Key_created) throw new NullReferenceException("App - Issue in generating hased bytes of Key");

// start mail service
Mail_Service.Set_Mail_Info(Web_builder, "Test Arena");

// change in settings of web app
Web_builder.Services.AddOpenApi(); // create doc for all endpoints
Web_builder.Services.ConfigureHttpJsonOptions((Options) => { Options.SerializerOptions.IncludeFields = true; });
Web_builder.Services.AddDbContext<App_DB_Context>(Options => Options.UseSqlite("Data Source=" + DB_File_Path));
Web_builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme) // jwt authentication of specific mapped calls
.AddJwtBearer(Options =>
{
    // not understood much but will some time
    Options.RequireHttpsMetadata = false;
    Options.SaveToken = true;
    Options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Secret_hash), // we used one in encryption so here so it can decrypt
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});
Web_builder.Services.AddAuthorization(); // for checking func needs permission
Web_builder.Services.AddRateLimiter(Opts =>
{
    // req which needs auth, thier time window based limit
    Opts.AddFixedWindowLimiter("StrictAuth", Options =>
        {
            Options.PermitLimit = 500; // req which need author. can be requested 5 times per given window time
            Options.Window = TimeSpan.FromMinutes(1); // refresh after 1 min
            Options.QueueLimit = 0; // no queing the requests
        });

    // how many req to handle ffrom 1 user at same time
    Opts.AddConcurrencyLimiter("SingleInFlight", Opts =>
    {
        Opts.PermitLimit = 500; // 1 req at time 
        Opts.QueueLimit = 0; // again no queing the reqs
    });

    // general api usage for all public(non auth end points)
    Opts.AddSlidingWindowLimiter("GeneralApi", Opts =>
    {
        Opts.PermitLimit = 100; // 50 reqs allowed 
        Opts.Window = TimeSpan.FromSeconds(10); // per 10 secs
        Opts.SegmentsPerWindow = 2; // divides window to segments of equal time
        Opts.QueueLimit = 0; // do not store multiple req from 1 suer
    });
});

// web app ready
var Web_app = Web_builder.Build();

// kind of embed front end into this
Web_app.UseDefaultFiles(); // Looks for index.html in wwwroot and serves it at root "/"
Web_app.UseStaticFiles();  // Serves css, js, images from wwwroot

// gen doc for all endpoints
Web_app.MapOpenApi();
Web_app.UseRateLimiter(); // app usage limits

// create minial schema of db in db file so db funcs can work later
using (var Scope = Web_app.Services.CreateScope())
{
    var Db = Scope.ServiceProvider.GetRequiredService<App_DB_Context>();
    Db.Database.EnsureCreated();
}

// actually use auth and author which added above
Web_app.UseAuthentication(); // must come 1st, who are you
Web_app.UseAuthorization(); // are you allowed

// register new user
Web_app.MapPost(End_Points.Register_User, async (Registration_Credentials Data, App_DB_Context Db) =>
{
    // basic verify if mail id if correct format
    if (!App_Funcs.Verify_Registration_Data(Data))
        return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // start process adncheck if complete
    var User_data = await App_Funcs.User_Registration(Data, Db, Secret_hash);
    if (User_data.Item1 != App_State.Op_Success) return App_Funcs.Get_Error_Msg(User_data.Item1);

    return Results.Ok();
});

// login user
Web_app.MapPost(End_Points.Login_User, async (Login_Credentials Data, App_DB_Context Db) =>
{
    // basic verify if mail id if correct format
    if (!App_Funcs.Verify_Login_Data(Data))
        return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // start process and check
    var User_data = await App_Funcs.User_Login(Data, Db, Secret_hash);
    if (User_data.Item1 != App_State.Op_Success) return App_Funcs.Get_Error_Msg(User_data.Item1);

    // make token
    var Token = Web_Utility.Gen_JWT_Token(User_data.Item2, Secret_hash, 300, out var completed);
    if (!completed) return App_Funcs.Get_Error_Msg(App_State.OTP_Gen_Failed);
    
    // send toek back
    return Results.Ok(new Authurized_Token(Token));
});

// forgot password
Web_app.MapPost(End_Points.Forgot_Password, async (Password_Update_Data Data, App_DB_Context Db) =>
{
    // basic verify if mail id if correct format
    if (Data is null || string.IsNullOrEmpty(Data.Email_id) || string.IsNullOrEmpty(Data.Password)
        || !MailAddress.TryCreate(Data.Email_id, out var Addr))
        return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    var State = await App_Funcs.Update_Password(Data, Secret_hash, Db);
    if (State != App_State.Op_Success) return App_Funcs.Get_Error_Msg(State);

    return Results.Ok();
});

// sending otp
Web_app.MapPost(End_Points.Send_OTP, async (OTP_Req Req) =>
{
    // basic verify if mail id if correct format
    if (string.IsNullOrEmpty(Req.Email_id) || !MailAddress.TryCreate(Req.Email_id, out var Addr))
        return Results.BadRequest("Email addr is not valid");

    string Name;
    if (string.IsNullOrEmpty(Req.Name)) Name = "User";
    else Name = Req.Name;

    // check if otp already pending in this email
    var Pending = App_Funcs.OTP_Already_Pending(Req.Email_id);
    if (Pending) return Results.Problem("OTP already exists for this email");

    // add empty otp and update later so mutiple req not passes Pending check
    App_Funcs.Add_New_OTP(Req.Email_id, 0, Req.Otp_type);

    // send otp
    var Otp = Mail_Service.Gen_OTP();
    if (!await Mail_Service.Send_OTP(Req.Email_id, Otp, Name))
        return App_Funcs.Get_Error_Msg(App_State.OTP_Gen_Failed);

    // add to app so it can check
    var Added = App_Funcs.Update_OTP(Req.Email_id, Otp, Req.Otp_type);
    if (!Added) App_Funcs.Get_Error_Msg(App_State.OTP_Gen_Failed);

    return Results.Ok();
});

// create room, that ClaimPrincipal is jwt token used for verification
Web_app.MapPost(End_Points.Create_Room, (Room_Creation_Info Info, ClaimsPrincipal User) =>
{
    // db id which was added to token as claim type name
    var Db_id_str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Db_id_str is null) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    var Parsed = int.TryParse(Db_id_str, out int Db_id);
    if (!Parsed) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    // return room id and they already have password
    return App_Funcs.Create_Room(Db_id, Info);
}).RequireAuthorization();

// starting room
Web_app.MapPost(End_Points.Start_Room, (Room_Joining_Info Info, ClaimsPrincipal User) =>
{
    if (Info is null || User is null || string.IsNullOrEmpty(Info.Name)) 
    return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // db id which was added to token as claim type name
    var Db_id_str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Db_id_str is null) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    var Parsed = int.TryParse(Db_id_str, out int Db_id);
    if (!Parsed) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    // return room id and they already have password
    var Start_state = App_Funcs.Start_Room(Db_id, Info);
    if (Start_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Start_state);

    return Results.Ok();

}).RequireAuthorization();

// join room
Web_app.MapPost(End_Points.Join_Room, (Room_Joining_Info Info, ClaimsPrincipal User) =>
{
    if (Info is null || User is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // db id which was added to token as claim type name
    var Db_id_str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Db_id_str is null) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    var Parsed = int.TryParse(Db_id_str, out int Db_id);
    if (!Parsed) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    // return room id and they already have password
    var Start_state = App_Funcs.Join_Room(Db_id, Info);
    if (Start_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Start_state);

    return Results.Ok();

}).RequireAuthorization();

// leave room
Web_app.MapPatch(End_Points.Leave_Room, (Room_Joining_Info Info, ClaimsPrincipal User) =>
{
    if (Info is null || User is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // db id which was added to token as claim type name
    var Db_id_str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Db_id_str is null) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    var Parsed = int.TryParse(Db_id_str, out int Db_id);
    if (!Parsed) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    // return room id and they already have password
    var Start_state = App_Funcs.Leave_Room(Db_id, Info);
    if (Start_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Start_state);

    return Results.Ok();
}).RequireAuthorization();

// updating test paper
Web_app.MapPatch(End_Points.Upload_Test_Json, (Test_Paper Test, ClaimsPrincipal User) =>
{
    if (Test is null || User is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // db id which was added to token as claim type name
    var Db_id_str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Db_id_str is null) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    // parse to int
    var Parsed = int.TryParse(Db_id_str, out int Db_id);
    if (!Parsed) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    // return room id and they already have password
    var Start_state = App_Funcs.Upload_Test_Json(Db_id, Test);
    if (Start_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Start_state);

    return Results.Ok();
}).RequireAuthorization();

// questions and options for test
Web_app.MapPost(End_Points.Get_Questions, (Room_Joining_Info Info, ClaimsPrincipal User) =>
{
    // db id
    var Db_id_str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Db_id_str is null) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    var Parsed = int.TryParse(Db_id_str, out int Db_id);
    if (!Parsed) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    // some checks
    if (Info is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // get the result
    var Result_state = App_Funcs.Get_Question_Paper(Info, out var Ques);
    if (Result_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Result_state);

    // send score back
    return Results.Ok(Ques);

}).RequireAuthorization();

// submiting test
Web_app.MapPost(End_Points.Sumbit_Test, (Test_Ans Ans, ClaimsPrincipal User) =>
{
    // db id
    var Db_id_str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Db_id_str is null) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    var Parsed = int.TryParse(Db_id_str, out int Db_id);
    if (!Parsed) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    // some checks
    if (Ans is null || Ans.Answers is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // get the result
    var Result_state = App_Funcs.Sumbit_Test(Db_id, Ans, out var Score);
    if (Result_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Result_state);

    // send score back
    return Results.Ok(Score);

}).RequireAuthorization();

// leaderboard and all done
Web_app.MapPost(End_Points.Get_LeaderBoard, (Room_Joining_Info Info, ClaimsPrincipal User) =>
{
    // db id
    var Db_id_str = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Db_id_str is null) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    var Parsed = int.TryParse(Db_id_str, out int Db_id);
    if (!Parsed) return App_Funcs.Get_Error_Msg(App_State.Token_Data_Null);

    // some checks
    if (Info is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // get the result
    var Result_state = App_Funcs.Get_Leaderboard(Db_id, Info, out var Scores);
    if (Result_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Result_state);

    // send score back
    return Results.Ok(Scores);

}).RequireAuthorization();

// room room room
Web_app.Run();

