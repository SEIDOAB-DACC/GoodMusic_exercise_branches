using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;

using Configuration.Options;

namespace Configuration;

public class JwtToken
{
    public string EncryptedToken { get; set; }

#if DEBUG
    public Guid TokenId { get; set; }
    public DateTime ExpireTime { get; set; }
    public IDictionary<string, string> UserClaims { get; set; }
#endif
}