using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp.Account.Web;
using Volo.Abp.Identity;

namespace Eksabli.Pages.Account;

// Code-behind override (same view-override mechanism as Login.cshtml itself — a class at the
// identical virtual path takes precedence over the compiled one in Volo.Abp.Account.Web) for exactly
// one gap: EmployeeAssignmentAppService.InviteAsync sets IdentityUser.ShouldChangePasswordOnNextLogin
// on every newly invited staff account, but the STOCK LoginModel.OnPostAsync has no branch for it at
// all — when SignInManager reports SignInResult.NotAllowed and the password given IS the correct one,
// it only ever distinguishes "must (periodically) change password" from "actually blocked" to choose
// between the same two dead-end alerts ("Invalid username or password!" vs "LoginIsNotAllowed"); it
// never redirects anywhere a temp password could actually be changed. That flow exists only on the
// OpenIddict /connect/token password-grant path (TokenController.HandleShouldChangePasswordOnNextLogin
// Async, via a ChangePasswordToken exchange) — irrelevant here since this app's Angular Business
// Portal uses the Authorization Code flow (environment.ts: responseType: 'code'), which always lands
// on THIS Razor page, not that endpoint. Confirmed live: an invited employee's very first login
// attempt, with the correct temp password, hit the exact "You are not allowed to log in!" dead end.
//
// Fix: intercept here, before any of that stock logic runs, and redirect to a new page
// (SetInitialPassword) built for exactly this — enter the temp password once more alongside a real
// new one, then sign in for real. Falls through to the stock OnPostAsync unchanged for every other
// case (wrong password, lockout, 2FA, external login, ordinary success), so nothing else about the
// login flow changes.
public class LoginModel : Volo.Abp.Account.Web.Pages.Account.LoginModel
{
    public LoginModel(
        IAuthenticationSchemeProvider schemeProvider,
        IOptions<AbpAccountOptions> accountOptions,
        IOptions<IdentityOptions> identityOptions,
        IdentityDynamicClaimsPrincipalContributorCache identityDynamicClaimsPrincipalContributorCache,
        IWebHostEnvironment webHostEnvironment)
        : base(schemeProvider, accountOptions, identityOptions, identityDynamicClaimsPrincipalContributorCache, webHostEnvironment)
    {
    }

    public override async Task<IActionResult> OnPostAsync(string action)
    {
        if (action == "Login" &&
            !string.IsNullOrWhiteSpace(LoginInput?.UserNameOrEmailAddress) &&
            !string.IsNullOrWhiteSpace(LoginInput?.Password))
        {
            var user = await UserManager.FindByNameAsync(LoginInput.UserNameOrEmailAddress)
                ?? await UserManager.FindByEmailAsync(LoginInput.UserNameOrEmailAddress);

            // Re-checking the password here (rather than trusting a later SignInResult.NotAllowed)
            // is deliberate: it keeps "wrong temp password" behaving exactly like the stock page
            // always has (falls through to base.OnPostAsync's own "Invalid username or password!"),
            // and only diverts the one case the stock page has no answer for.
            if (user != null &&
                user.ShouldChangePasswordOnNextLogin &&
                await UserManager.CheckPasswordAsync(user, LoginInput.Password))
            {
                return RedirectToPage("./SetInitialPassword", new
                {
                    userId = user.Id,
                    returnUrl = ReturnUrl,
                    returnUrlHash = ReturnUrlHash,
                });
            }
        }

        return await base.OnPostAsync(action);
    }
}
