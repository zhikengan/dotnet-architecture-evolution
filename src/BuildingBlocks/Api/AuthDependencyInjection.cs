using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Infrastructure.Authentication;
using BuildingBlocks.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Api;

/// <summary>
/// Composition for authentication and authorization, exposed by the shared
/// kernel so every host wires it identically. Tier 4 graduated from HS256
/// to <strong>RS256 with discovery</strong>: the host signs with the private
/// key, validation reads the public key, and the JWKS endpoint publishes
/// the same key under its <c>KeyId</c> so relying parties (Worker, future
/// SDKs) can validate without ever holding the signing material.
/// </summary>
public static class AuthDependencyInjection
{
    public static IServiceCollection AddMarketplaceAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<JwtPublicKeyProvider>();
        services.AddSingleton<JwtTokenIssuer>();

        // Scoped tenant context — same instance resolves via both interfaces.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configure JwtBearer with access to the public-key provider via DI.
        // Using a typed configurator (rather than a closure-captured key) keeps
        // the validation key in sync with whatever the JwtPublicKeyProvider
        // currently holds and supports rotation later without ceremony.
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Buyer", p => p.RequireAuthenticatedUser().RequireRole("Buyer"));
            options.AddPolicy("Seller", p => p.RequireAuthenticatedUser().RequireRole("Seller"));
            options.AddPolicy("Admin", p => p.RequireAuthenticatedUser().RequireRole("Admin"));
        });

        return services;
    }

    private sealed class ConfigureJwtBearer(IOptions<JwtOptions> jwtOptions, JwtPublicKeyProvider keyProvider)
        : IConfigureNamedOptions<JwtBearerOptions>
    {
        public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

        public void Configure(string? name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme) return;
            var jwt = jwtOptions.Value;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = keyProvider.SecurityKey,
                ClockSkew = TimeSpan.FromSeconds(5),
            };
        }
    }
}
