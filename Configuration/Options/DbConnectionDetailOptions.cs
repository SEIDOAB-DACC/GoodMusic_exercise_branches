namespace Configuration.Options;

public class DbConnectionDetailOptions
{   
    public string DbUserLogin { get; set; }
    public string DbConnection { get; set; }
    public string DbConnectionString {get; init;}
}