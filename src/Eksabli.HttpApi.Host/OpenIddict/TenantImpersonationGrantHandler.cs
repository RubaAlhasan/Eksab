using System;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.Businesses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.ExtensionGrantTypes;

namespace Eksabli.OpenIddict;

// Custom OpenIddict grant type ("grant_type=impersonation") — redeems a single-use code minted by
// AdminTenantAppService.GetImpersonationTokenAsync (Eksabli.Tenants.Impersonate) for a real access
// token as the target tenant's own "admin" user, so a platform admin can act as that business
// without knowing its password. Same two-step "mint a short-lived code, redeem it here" shape as
// OtpLoginGrantHandler's own "otp" grant right below it in this folder — see that file's comment for
// why a custom grant, not a plain REST endpoint, is the right tool for actually minting a token.
//
// Constructed via `new TenantImpersonationGrantHandler()` directly in
// EksabliHttpApiHostModule.ConfigureExtensionGrants (not DI-resolved), so it takes no constructor
// dependencies — everything is resolved lazily inside HandleAsync via
// context.HttpContext.RequestServices, same as OtpLoginGrantHandler.
public class TenantImpersonationGrantHandler : ITokenExtensionGrant
{
    public string Name => "impersonation";

    public async Task<IActionResult> HandleAsync(ExtensionGrantContext context)
    {
        var services = context.HttpContext.RequestServices;

        var code = context.Request.GetParameter("code")?.ToString();
        if (string.IsNullOrWhiteSpace(code))
        {
            return BuildErrorResult(OpenIddictConstants.Errors.InvalidRequest, "code is required.");
        }

        var cache = services.GetRequiredService<IDistributedCache>();
        var cacheKey = ImpersonationTokenCacheItem.CacheKeyPrefix + code;
        var cachedBytes = await cache.GetAsync(cacheKey);
        if (cachedBytes == null)
        {
            return BuildErrorResult(OpenIddictConstants.Errors.InvalidGrant, "The code has expired.");
        }

        await cache.RemoveAsync(cacheKey); // single-use — burn on first read, same as OtpCacheItem

        var item = JsonSerializer.Deserialize<ImpersonationTokenCacheItem>(cachedBytes)!;

        Volo.Abp.Identity.IdentityUser? targetUser;
        var currentTenant = services.GetRequiredService<ICurrentTenant>();
        using (currentTenant.Change(item.TenantId))
        {
            // Same lookup BusinessAppService.RegisterAsync uses right after provisioning a tenant —
            // "admin" is the fixed username IdentityDataSeedContributor gives every tenant's owner.
            var identityUserRepository = services.GetRequiredService<IIdentityUserRepository>();
            targetUser = await identityUserRepository.FindByNormalizedUserNameAsync("ADMIN");
        }

        if (targetUser == null)
        {
            return BuildErrorResult(OpenIddictConstants.Errors.InvalidGrant, "This business has no admin user to impersonate.");
        }

        var principal = await BuildPrincipalAsync(services, context.Request, targetUser);

        return new Microsoft.AspNetCore.Mvc.SignInResult(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal);
    }

    private static ForbidResult BuildErrorResult(string error, string description)
    {
        var properties = new AuthenticationProperties(new System.Collections.Generic.Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        });

        return new ForbidResult(new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme }, properties);
    }

    // Identical shape to OtpLoginGrantHandler.BuildPrincipalAsync — see that file's own comments for
    // why each step is needed (aud claim, resources, dynamic claims).
    private static async Task<System.Security.Claims.ClaimsPrincipal> BuildPrincipalAsync(
        IServiceProvider services,
        OpenIddictRequest request,
        Volo.Abp.Identity.IdentityUser user)
    {
        var claimsPrincipalFactory = services.GetRequiredService<IUserClaimsPrincipalFactory<Volo.Abp.Identity.IdentityUser>>();
        var principal = await claimsPrincipalFactory.CreateAsync(user);

        principal.SetScopes(request.GetScopes());

        var scopeManager = services.GetRequiredService<IOpenIddictScopeManager>();
        var resources = new System.Collections.Generic.List<string>();
        await foreach (var resource in scopeManager.ListResourcesAsync(principal.GetScopes()))
        {
            resources.Add(resource);
        }

        principal.SetResources(resources);

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        }

        var claimsPrincipalManager = services.GetRequiredService<AbpOpenIddictClaimsPrincipalManager>();
        await claimsPrincipalManager.HandleAsync(request, principal);

        return principal;
    }
}
