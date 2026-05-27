using System.Text;
using BuildingBlocks.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Api;

/// <summary>
/// Composition for authentication and authorization, exposed by the shared
/// kernel so every host wires it identically. Binds <see cref="JwtOptions"/>,
/// registers the token issuer, configures JwtBearer validation, and declares
/// the three role policies endpoint groups consume.
/// </summary>
public static class AuthDependencyInjection
{
    public static IServiceCollection AddMarketplaceAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<JwtTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Resolve options lazily so WebApplicationFactory-style test
                // overrides applied via ConfigureAppConfiguration are visible
                // when JwtBearerOptions is materialized — same lazy pattern
                // module DI extensions use for the connection string.
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? throw new InvalidOperationException("Jwt configuration section is required");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromSeconds(5),
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Buyer", p => p.RequireAuthenticatedUser().RequireRole("Buyer"));
            options.AddPolicy("Seller", p => p.RequireAuthenticatedUser().RequireRole("Seller"));
            options.AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireRole("Admin"));
        });

        return services;
    }
}
