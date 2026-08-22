using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

public class User_Data_Args
{
    public required int DB_id;
    public required string Encrypted_email_id, Hashed_email_id,
    Hashed_password, Name;
}

// storing info of user
[Index(nameof(Hashed_email_id), IsUnique = true)]
public sealed class User_Data
{
    // Vars ------------------------------------------

    [Key] public int DB_id { get; set; } = 0;
    public string Name { get; set; } = "";
    public string Hashed_email_id { get; set; } = ""; // 2nd key
    public string Encrypted_email_id { get; set; } = "";
    public string Hashed_password { get; set; } = "";

    // Funcs -----------------------------------

    public User_Data() { }

    /// <param name="DB_id">Shall be gen by sql/etc</param>
    /// <param name="Email_Id">Must be encrypted</param>
    /// <param name="Hashed_password"></param>
    public User_Data(User_Data_Args Args)
    {
        DB_id = Args.DB_id;
        Encrypted_email_id = Args.Encrypted_email_id;
        Hashed_email_id = Args.Hashed_email_id;
        Hashed_password = Args.Hashed_password;
        Name = Args.Name;
    }
}
