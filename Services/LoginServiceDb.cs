using Microsoft.Extensions.Logging;

using DbRepos;
using Models.DTO;
using DbContext;
using System.Security.Claims;
using Configuration;

namespace Services;

public class LoginServiceDb : ILoginService
{
    private readonly LoginDbRepos _repo;
    private readonly JwtEncryptions _jtwEncryptions;
    private readonly ILogger<LoginServiceDb> _logger;

    public LoginServiceDb(ILogger<LoginServiceDb> logger, LoginDbRepos repo, JwtEncryptions jtwEncryptions)
    {
        _repo = repo;
        _logger = logger;
        _jtwEncryptions = jtwEncryptions;
    }

    public async Task<ResponseItemDto<LoginUserSessionDto>> LoginUserAsync(LoginCredentialsDto usrCreds)
    {
        try
        {
            var _usrSession = await _repo.LoginUserAsync(usrCreds);

            // Successful login. Create a JWT token
            // Add user claims if any
            IDictionary<string, string> userClaims = new Dictionary<string, string>();
            userClaims["UserId"] = _usrSession.Item.UserId.ToString();
            userClaims["UserRole"] = _usrSession.Item.UserRole;
            userClaims["UserName"] = _usrSession.Item.UserName;

            _usrSession.Item.JwtToken = _jtwEncryptions.CreateToken(_usrSession.Item.UserRole, userClaims);

#if DEBUG
            //For test only, decypt the JWT token and compare.
            var claims = _jtwEncryptions.GetClaimsFromToken(_usrSession.Item.JwtToken.EncryptedToken);
            if (claims["UserId"] != _usrSession.Item.UserId.ToString()) throw new InvalidOperationException("JWT token decryption failed - UserId mismatch");
#endif
            return _usrSession;
        }
        catch
        {
            //if there was an error during login, simply pass it on.
            throw;
        }
    }
}

