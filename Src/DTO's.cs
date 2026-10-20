sealed record User_ID(uint Id);

sealed record Test_Paper
{
    // Vars --------------------------------

    public readonly string[] Questions;
    public readonly string[] Options;
    public readonly int[] Ans;

    public const byte Options_Per_Que = 4;

    // Funcs -------------------------------

    public Test_Paper(string[] Questions, string[] Options, int[] Ans)
    {
        // broad check
        if (Questions is null || Options is null || Ans is null
        || Questions.Length is < 1 or > 500 || Options.Length != Questions.Length << 2
        || Ans.Length != Questions.Length)
            throw new ArgumentException("Test Paper - wrong format");

        // deep checks
        for (int i = 0; i < Ans.Length; i++)
        {
            if (Ans[i] is >= 4) throw new ArgumentException("Test Paper - answers are wrong values");
            if (Questions[i] is null || Questions[i].Length == 0) throw new ArgumentException("Test Paper - questions are wrong values");
        }

        // init
        this.Questions = new string[Questions.Length];
        this.Ans = new int[Ans.Length];
        this.Options = new string[Options.Length];

        // set vals
        Array.Copy(Questions, this.Questions, this.Questions.Length);
        Array.Copy(Options, this.Options, this.Options.Length);
        Array.Copy(Ans, this.Ans, this.Ans.Length);
    }

    /// <param name="Que_num">0 based</param>
    /// <returns>index of 1st que</returns>
    /// <exception cref="ArgumentException"></exception>
    public ushort Options_Of(ushort Que_num)
    {
        if (Que_num >= Questions.Length)
            throw new ArgumentException("Que not exists");

        return (ushort)(Que_num * 4);
    }
}

sealed record Room_Creation_Info
(
    // 180 min max
    User_ID User_id,
    byte Minutes, ushort Password,
    byte Positve_marks, byte Negative_marks,
    int Test_taker_count, string Creator_name,
    Test_Paper Test_paper
);

sealed record Room_Joining_Info // for joining
(
    User_ID User_id, int Room_id, int Password, string Name
);

sealed record Questions_Set // for sending user the questions
(
    string[] Questions,
    string[] Options
);

sealed record Test_Ans(User_ID User_id, int[] Answers, int Room_id, int Password);

sealed record Test_Leaderboard
(
    List<string> Names_and_scores
);
