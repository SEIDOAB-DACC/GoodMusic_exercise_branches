using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;

using Configuration.Options;

namespace Configuration.Extensions;

public static class JWTEncryptionsExtensions
{
    public static void AddJwtToken(this IServiceCollection Services, IConfiguration configuration)
    {
        //Here we tell ASP.NET Core that we are using JWT Authentication
        Services.AddAuthentication(options => {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })

            //Here we tell ASP.NET Core that it will be JWT Bearer token based
            .AddJwtBearer(options => {
                
                var jwtOptions = configuration.GetSection(JwtOptions.Position).Get<JwtOptions>();
                
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;

                //Here we tell ASP.NET Core how to validate the JWT in the HTTP request pipeline
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
                {
                    ValidateIssuerSigningKey = jwtOptions.ValidateIssuerSigningKey,
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtOptions.IssuerSigningKey)),
                    ValidateIssuer = jwtOptions.ValidateIssuer,
                    ValidIssuer = jwtOptions.ValidIssuer,
                    ValidateAudience = jwtOptions.ValidateAudience,
                    ValidAudience = jwtOptions.ValidAudience,
                    RequireExpirationTime = jwtOptions.RequireExpirationTime,
                    ValidateLifetime = jwtOptions.RequireExpirationTime,
                    ClockSkew = TimeSpan.FromDays(1),
                };
        });
        
        Services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Position));
        Services.AddTransient<JwtEncryptions>();
    }
}
