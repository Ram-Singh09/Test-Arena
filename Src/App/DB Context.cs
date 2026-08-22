using Microsoft.EntityFrameworkCore;

public class App_DB_Context : DbContext
{
    // creates set
    public DbSet<User_Data> Users => Set<User_Data>();

    // telling EF core which calss it will manage and its settings
    public App_DB_Context(DbContextOptions<App_DB_Context> Options) : base(Options)
    {

    }
}
