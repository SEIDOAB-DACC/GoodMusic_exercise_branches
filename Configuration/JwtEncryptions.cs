using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;

using Configuration.Options;

namespace Configuration;

public class JwtEncryptions
{
    private readonly JwtOptions _jwtOptions;

    public JwtEncryptions(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    //Create a list of claims to encrypt into the JWT token
    private IEnumerable<Claim> CreateClaims(Guid TokenId, string Role, IDictionary<string, string> userClaims)
    {
        IEnumerable<Claim> claims = new List<Claim>();
        foreach (var kvp in userClaims)
        {
            claims = claims.Append(new Claim(kvp.Key, kvp.Value));
        }

        //Add standard claims used by Microsoft.AspNetCore.Authentication and used in the HTTP request pipeline
        claims = claims.Append(new Claim(ClaimTypes.Expiration, DateTime.UtcNow.AddMinutes(_jwtOptions.LifeTimeMinutes).ToString("MMM ddd dd yyyy HH:mm:ss tt")));
        claims = claims.Append(new Claim(ClaimTypes.NameIdentifier, TokenId.ToString()));
        claims = claims.Append(new Claim(ClaimTypes.Role, Role));
        return claims;
    }

    public JwtToken CreateToken(string Role, IDictionary<string, string> userClaims)
    {
        var token = new JwtToken();
        Guid tokenId = Guid.NewGuid();

        //get the encryption key from user-secrets and set token expiration time
        var encryptionKey = System.Text.Encoding.ASCII.GetBytes(_jwtOptions.IssuerSigningKey);
        DateTime expireTime = DateTime.UtcNow.AddMinutes(_jwtOptions.LifeTimeMinutes);

        //generate the token, including my own defined claims, expiration time, signing credentials
        var JWToken = new JwtSecurityToken(issuer: _jwtOptions.ValidIssuer,
            audience: _jwtOptions.ValidAudience,
            claims: CreateClaims(tokenId, Role, userClaims),
            notBefore: new DateTimeOffset(DateTime.UtcNow).DateTime,
            expires: new DateTimeOffset(expireTime).DateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(encryptionKey), SecurityAlgorithms.HmacSha256));

        token.EncryptedToken = new JwtSecurityTokenHandler().WriteToken(JWToken);

#if DEBUG
        token.UserClaims = userClaims;
        token.TokenId = tokenId;
        token.ExpireTime = expireTime;
#endif
        return token;
    }

    public IDictionary<string, string> GetClaimsFromToken(string _encryptedtoken)
    {
        if (_encryptedtoken == null) return null;

        var _decodedToken = new JwtSecurityTokenHandler().ReadJwtToken(_encryptedtoken);
        return _decodedToken?.Claims?.ToDictionary(c => c.Type, c => c.Value);
    }
}