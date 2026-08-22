using System.Collections.Concurrent;

sealed class Test_Taker
{
    public readonly string Name;
    public bool Sumbited = false;
    public int Score = 0;

    public Test_Taker(string Name)
    {
        this.Name = Name;
    }
}

sealed class Test_Room
{
    // Vars -------------------------------

    public readonly int Password;
    public Test_Paper Test_paper;
    public DateTime Finish_time;

    public readonly byte Positve_marks, Negative_marks, Duration;
    public readonly ConcurrentDictionary<int, Test_Taker> Test_takers; // key id db id

    public bool Started = false;
    public Test_Leaderboard Leaderboard;
    private readonly Lock _Lock = new();

    // Funcs ------------------------------

    public Test_Room(int Db_id, Room_Creation_Info Info)
    {
        Password = Info.Password;
        Test_paper = Info.Test_paper;
        Duration = Info.Minutes > 180 ? (byte)180 : Info.Minutes;
        Positve_marks = Info.Positve_marks;
        Negative_marks = Info.Negative_marks;
        Test_takers = new();
        Leaderboard = new(new(4));

        Test_takers.TryAdd(Db_id, new(Info.Creator_name));
    }

    public App_State Start()
    {
        if (Started && DateTime.UtcNow > Finish_time) return App_State.Room_Closed;
        else if (Started) return App_State.Room_Already_Started;

        Started = true;
        Finish_time = DateTime.UtcNow.AddMinutes(Duration);

        return App_State.Op_Success;
    }

    // when test is submited
    public App_State Evaluate(int Db_id, int[] Answers, out int Score)
    {
        Score = 0;
        Test_Taker Test_taker;

        // some checks
        if (!Started) return App_State.Room_Not_Started;
        else if (DateTime.UtcNow > Finish_time) return App_State.Test_Completed;
        else if (!Test_takers.TryGetValue(Db_id, out Test_taker!)) return App_State.Test_Taker_Not_Found;
        else if (Test_taker.Sumbited) return App_State.Test_Already_Submited;

        // shall not be evaluated again
        Test_taker.Sumbited = true;

        // 0 to 3 are ans aobve that if not attempted, using answer len 
        // because not attempted que can be removed from array to reduce badnwidth
        for (int i = 0; i < Answers.Length; i++)
        {
            // que was skipped
            if (Answers[i] > 3) continue;

            // que was answered
            Score += (Answers[i] == Test_paper.Ans[i]) ? Positve_marks : -Negative_marks;
        }

        // set score and done
        Test_taker.Score = Score;

        // add to leaderboard
        lock (_Lock)
        {
            Leaderboard.Names_and_scores.Add(Test_taker.Name + $": {Score}");
        }

        return App_State.Op_Success;
    }

    /// <param name="Db_id">Db id of who is joining</param>
    public App_State Add_Test_Taker(int Db_id, string Name)
    {
        // try find user in db/ removed it as if it passed jwt and not in db then issue something else
        //var User = await Db.Users.FindAsync(Db_id);
        //if (User is null) return App_State.User_Not_Exists;

        if (Started && DateTime.UtcNow > Finish_time) return App_State.Room_Closed;

        // add and done
        if (!Test_takers.TryAdd(Db_id, new(Name))) return App_State.Already_Part_Of_Room;

        return App_State.Op_Success;
    }

    /// <param name="Db_id">Db id of who is joining</param>
    public App_State Remove_Test_Taker(int Db_id)
    {
        if (Started && DateTime.UtcNow > Finish_time) return App_State.Room_Closed;

        if (!Test_takers.TryRemove(Db_id, out var Taker))
            return App_State.Not_Part_Of_Room;

        return App_State.Op_Success;
    }

    public App_State Update_Test(Test_Paper Test)
    {
        if (Started && DateTime.UtcNow > Finish_time) return App_State.Room_Closed;
        else if (Started) return App_State.Room_Already_Started;

        Test_paper = Test is not null ? Test : Test_paper;

        return App_State.Op_Success;
    }
}

static class Test_Room_Manager
{
    // Vars --------------------------

    // used to find which room is owned by specific user
    private static ConcurrentDictionary<int, int> Db_id_to_room_id = new();

    // keeps track of rooms by room id and used for joining
    private static ConcurrentDictionary<int, Test_Room> Room_id_to_room = new();

    private static int Room_Counter = 0;

    // Funcs -------------------------

    public static int Create_Room(int Db_id, Room_Creation_Info Info, out bool Success)
    {
        Success = false;

        // increase safely
        var Room_id = Interlocked.Increment(ref Room_Counter);
        int Old_room_id = 0;

        // will change value
        Db_id_to_room_id.AddOrUpdate(Db_id, Room_id, (Key, Prev_room_id) =>
        {
            Old_room_id = Prev_room_id;
            return Room_id; // if updating it takes this val else 2nd arg of func
        });

        // remove old one, failed means already removed no issue
        if (Old_room_id != 0)
            Room_id_to_room.TryRemove(Old_room_id, out var _);

        // try add room with new id which shall be parfect
        Success = Room_id_to_room.TryAdd(Room_id, new(Db_id, Info));

        return Room_id;
    }

    public static (App_State, Test_Room?) Get_Room(int Db_id)
    {
        // if user not exists in table of creators then its room not exists
        var Room_exists = Db_id_to_room_id.TryGetValue(Db_id, out var Room_id);
        if (!Room_exists) return (App_State.Room_Not_Exists, null);

        //safely check room exists
        var Room_found = Room_id_to_room.TryGetValue(Room_id, out var Room);
        if (!Room_found) return (App_State.Room_Not_Exists, null);

        return (App_State.Op_Success, Room);
    }

    public static (App_State, Test_Room?) Get_Room_Outsider(int Room_id)
    {
        // safely check room exists
        var Room_found = Room_id_to_room.TryGetValue(Room_id, out var Room);
        if (!Room_found) return (App_State.Room_Not_Exists, null);

        return (App_State.Op_Success, Room);
    }

}
