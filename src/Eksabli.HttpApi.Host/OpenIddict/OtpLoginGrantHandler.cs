using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Eksabli.Otp;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.ExtensionGrantTypes;

namespace Eksabli.OpenIddict;

// Custom OpenIddict grant type ("grant_type=otp") — exchanges a verified phone/OTP-code pair for
// an access token, creating the Host-realm customer (Volo.Abp.Identity.IdentityUser + CustomerProfile) on first use.
// See docs/eksabli-loyalty-platform/02-system-architecture.md#security.
//
// Constructed via `new OtpLoginGrantHandler()` directly in EksabliHttpApiHostModule.ConfigureServices
// (not DI-resolved), so it takes no constructor dependencies — everything is resolved lazily inside
// HandleAsync via context.HttpContext.RequestServices.
public class OtpLoginGrantHandler : ITokenExtensionGrant
{
    public string Name => "otp";

    public async Task<IActionResult> HandleAsync(ExtensionGrantContext context)
    {
        var services = context.HttpContext.RequestServices;

        var phoneNumber = context.Request.GetParameter("phone_number")?.ToString();
        var code = context.Request.GetParameter("otp_code")?.ToString();

        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(code))
        {
            return BuildErrorResult(OpenIddictConstants.Errors.InvalidRequest, "phone_number and otp_code are required.");
        }

        var otpLoginService = services.GetRequiredService<IOtpLoginService>();
        var result = await otpLoginService.ValidateAndResolveUserAsync(phoneNumber, code);

        if (!result.IsValid)
        {
            return BuildErrorResult(
                OpenIddictConstants.Errors.InvalidGrant,
                result.ErrorCode == "expired_code" ? "The code has expired." : "The code is invalid.");
        }

        var principal = await BuildPrincipalAsync(services, context.Request, result.User!);

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

    private static async Task<System.Security.Claims.ClaimsPrincipal> BuildPrincipalAsync(
        IServiceProvider services,
        OpenIddictRequest request,
        Volo.Abp.Identity.IdentityUser user)
    {
        var claimsPrincipalFactory = services.GetRequiredService<IUserClaimsPrincipalFactory<Volo.Abp.Identity.IdentityUser>>();
        var principal = await claimsPrincipalFactory.CreateAsync(user);

        principal.SetScopes(request.GetScopes());

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        }

        // Lets ABP apply any additional dynamic claims the same way built-in grants (e.g. Password) do.
        var claimsPrincipalManager = services.GetRequiredService<AbpOpenIddictClaimsPrincipalManager>();
        await claimsPrincipalManager.HandleAsync(request, principal);

        return principal;
    }
}
