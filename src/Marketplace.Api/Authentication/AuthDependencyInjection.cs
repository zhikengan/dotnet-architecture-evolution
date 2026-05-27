using System.Text;
using Marketplace.Application.Abstractions;
using Marketplace.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.Api.Authentication;

/// <summary>
/// API-layer composition for authentication and authorization. Binds
/// <see cref="JwtOptions"/>, registers the issuer, wires the JwtBearer
/// validation parameters, and declares three role policies (Buyer/Seller/Admin)
/// that endpoints consume via <c>.RequireAuthorization(name)</c>.
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
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Resolve options lazily so WebApplicationFactory test overrides
                // (e.g., in-memory config sources added in ConfigureAppConfiguration)
                // are visible by the time JwtBearerOptions is materialized.
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
