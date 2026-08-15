using Configuration;

namespace Models.DTO;

public class LoginCredentialsDto
{
    public string UserNameOrEmail { get; set; } 
    public string UserPassword { get; set; }
}

public class LoginUserSessionDto
{
    public Guid? UserId { get; set; }
    public string UserName { get; set; }
    public string UserRole { get; set; }
    public JwtToken JwtToken { get; set; }
}


