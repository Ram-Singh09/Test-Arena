using System.Collections.Concurrent;

// INCOMPLETE

struct RL_Active_Session
{
    public readonly int DB_id = 0, Mask;

    public RL_Active_Session(int DB_id, int Mask)
    {
        this.DB_id = DB_id;
        this.Mask = Mask;
    }
}

// state defaults at 0 in start
static class Request_Limiter
{
    // db id and 2nd is user info of state
    private static readonly ConcurrentDictionary<int, RL_Active_Session> User_reqs = new();
    private static Lock _Lock = new();
    public const byte Max_Bit = 31;

    /// <param name="Db_id">Adds to db if not already there</param>
    /// <param name="Bit">Max allowed upto 32 bits, 0 to 31</param>
    /// <returns>True if success else false</returns>
    public static bool Mark_Req(int Db_id, byte Bit_index)
    {
        if (Bit_index > Max_Bit) return false;

        // simply add as new or update prev one
        User_reqs.AddOrUpdate
        (
            Db_id,
            new RL_Active_Session(Db_id, 1 << Bit_index),
            (int Key, RL_Active_Session Val) =>
            {
                return new(Db_id, Val.Mask | (1 << Bit_index));
            }
        );
        
        return true;
    }

    /// <param name="Db_id">Adds to db if not already there</param>
    /// <param name="Bit">Max allowed upto 32 bits, 0 to 31</param>
    /// <returns>True if success else false</returns>
    public static bool UnMark_Req(int Db_id, byte Bit_index)
    {
        if (Bit_index > Max_Bit) return false;

        // simply add as new or update prev one
        User_reqs.AddOrUpdate
        (
            Db_id,
            new RL_Active_Session(Db_id, 0),
            (int Key, RL_Active_Session Val) =>
            {
                return new(Db_id, Val.Mask & (~(1 << Bit_index)));
            }
        );

        return true;
    }

    /// <summary>
    /// if not added then auto adds and marks that marks that bit on
    /// </summary>
    /// <returns>True if pending else false</returns>
    public static bool Is_Req_Pending_And_Mark(int Db_id, int Bit_index, out bool Bits_overflowed)
    {
        // simple bits check
        Bits_overflowed = true;
        if (Bit_index > Max_Bit) return false;

        Bits_overflowed = false;

        // if not exists add to list
        if (!User_reqs.TryGetValue(Db_id, out var Session))
        {
            User_reqs.AddOrUpdate
            (
                Db_id,
                new RL_Active_Session(Db_id, 1 << Bit_index),
                (int Key, RL_Active_Session Val) =>
                {
                    return new(Db_id, Val.Mask | (1 << Bit_index));
                }
            );

            // not exists means not pending
            return false;
        }

        // if already exists then just update
        else
        {

            // just in case god oggy gets angry and dds not update
            User_reqs.AddOrUpdate
            (
                Db_id,
                new RL_Active_Session(Db_id, Session.Mask | (1 << Bit_index)),
                (int Key, RL_Active_Session Val) =>
                {
                    return new(Db_id, Val.Mask | (1 << Bit_index));
                }
            );

            return (Session.Mask & (1 << Bit_index)) != 0;

        }

    }

    /// <summary>
    /// auto adds user to list for future operations
    /// </summary>
    /// <returns>True if exists and pending else false</returns>
    public static bool Is_Req_Pending(int Db_id, int Bit_index, out bool Bits_overflowed)
    {
        // simple bits check
        Bits_overflowed = true;
        if (Bit_index > Max_Bit) return false;

        Bits_overflowed = false;

        // if not exists add to list
        if (!User_reqs.TryGetValue(Db_id, out var Session))
        {
            User_reqs.AddOrUpdate
            (
                Db_id,
                new RL_Active_Session(Db_id, 0),
                (int Key, RL_Active_Session Val) =>
                {
                    return new(Db_id, Val.Mask);
                }
            );

            // not exists means not pending
            return false;
        }

        // if already exists then just check
        else return (Session.Mask & (1 << Bit_index)) != 0;
    }
}
