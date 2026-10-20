using Microsoft.AspNetCore.Http.Json;

// creates web app to allow changes in final web app
var Web_builder = WebApplication.CreateBuilder(args);
Web_builder.Services.ConfigureHttpJsonOptions((JsonOptions Opts) => { Opts.SerializerOptions.IncludeFields = true; });

// web app configuration
var Web_app = Web_builder.Build();
Web_app.UseDefaultFiles(); // Looks for index.html in wwwroot and serves it at root "/"
Web_app.UseStaticFiles();  // Serves css, js, images from wwwroot

uint Global_id = 0;
Lock Locker = new();

Web_app.MapGet(End_Points.Get_User_Id, () =>
{
    lock (Locker)
    {
        Global_id = Interlocked.Increment(ref Global_id);
        return Results.Ok(Global_id);
    }
});

Web_app.MapPost(End_Points.Create_Room, (Room_Creation_Info Info) =>
{
    return App_Funcs.Create_Room(Info.User_id, Info); 
});

Web_app.MapPost(End_Points.Join_Room, (Room_Joining_Info Info) =>
{
    if (Info is null || Info.User_id is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // return room id and they already have password
    var Start_state = App_Funcs.Join_Room(Info.User_id.Id, Info);
    if (Start_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Start_state);

    return Results.Ok();

});

Web_app.MapPatch(End_Points.Leave_Room, (Room_Joining_Info Info) =>
{
    if (Info is null || Info.User_id is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // return room id and they already have password
    var Start_state = App_Funcs.Leave_Room(Info.User_id.Id, Info);
    if (Start_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Start_state);

    return Results.Ok();
});

Web_app.MapPost(End_Points.Get_Questions, (Room_Joining_Info Info) =>
{
    if (Info is null || Info.User_id is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // get ques
    var Result_state = App_Funcs.Get_Question_Paper(Info, out var Ques);
    if (Result_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Result_state);

    // send ques
    return Results.Ok(Ques);
});

Web_app.MapPost(End_Points.Sumbit_Test, (Test_Ans Ans) =>
{
    // some checks
    if (Ans.User_id is null || Ans is null || Ans.Answers is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // get the result
    var Result_state = App_Funcs.Sumbit_Test(Ans.User_id.Id, Ans, out var Score);
    if (Result_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Result_state);

    // send score back
    return Results.Ok(Score);

});

Web_app.MapPost(End_Points.Get_LeaderBoard, (Room_Joining_Info Info) =>
{
    // some checks
    if (Info.User_id is null || Info is null) return App_Funcs.Get_Error_Msg(App_State.Null_Data_Error);

    // get the result
    var Result_state = App_Funcs.Get_Leaderboard(Info.User_id.Id, Info, out var Scores);
    if (Result_state != App_State.Op_Success) return App_Funcs.Get_Error_Msg(Result_state);

    // send score back
    return Results.Ok(Scores);

});

Web_app.MapGet(End_Points.Api_Paths, () => new
{
    End_Points.Get_User_Id, End_Points.Create_Room, End_Points.Join_Room,
    End_Points.Leave_Room, End_Points.Get_Questions,
    End_Points.Sumbit_Test, End_Points.Get_LeaderBoard
});

// room room room
Web_app.Run();

