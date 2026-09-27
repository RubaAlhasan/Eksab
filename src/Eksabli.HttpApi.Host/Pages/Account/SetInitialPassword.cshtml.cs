using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Account.Web.Pages.Account;
using Volo.Abp.Identity;
using Volo.Abp.Validation;

namespace Eksabli.Pages.Account;

// New page, not a stock ABP one — see Login.cshtml.cs's own header comment for why this exists: the
// stock login flow has no way for an invited staff account (ShouldChangePasswordOnNextLogin) to
// actually change their temp password when reached via the Authorization Code flow. This is that
// missing step — same shape as ABP's own ResetPassword page (current + new + confirm, one POST,
// then sign in for real), scoped narrowly to this one case rather than reused for
// ShouldPeriodicallyChangePassword too (nothing in this codebase sets that one yet).
public class SetInitialPasswordModel : AccountPageModel
{
    public class SetInitialPasswordInputModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxPasswordLength))]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword))]
        public string ConfirmNewPassword { get; set; }
    }

    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    // Nullable, not plain string — both are legitimately absent for a lot of real redirects (this
    // project's .csproj has Nullable enable, so a non-nullable string here gets an IMPLICIT
    // [Required] from ASP.NET Core's model validation; confirmed live: "The ReturnUrlHash field is
    // required" blocked every submission once the hidden input below started round-tripping its
    // empty value as "" rather than a genuinely missing field).
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrlHash { get; set; }

    [BindProperty]
    public SetInitialPasswordInputModel Input { get; set; } = new();

    public virtual async Task<IActionResult> OnGetAsync()
    {
        var user = await UserManager.FindByIdAsync(UserId.ToString());
        if (user == null || !user.ShouldChangePasswordOnNextLogin)
        {
            return RedirectToPage("./Login", new { ReturnUrl, ReturnUrlHash });
        }

        return Page();
    }

    public virtual async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await UserManager.FindByIdAsync(UserId.ToString());
        if (user == null || !user.ShouldChangePasswordOnNextLogin)
        {
            return RedirectToPage("./Login", new { ReturnUrl, ReturnUrlHash });
        }

        var result = await UserManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            // NOT CheckIdentityErrors here — that throws a UserFriendlyException, which is the right
            // pattern for an API controller (ABP's exception filter turns it into a JSON error
            // response) but wrong for a plain HTML form post: the user would just hit an unhandled
            // exception with nothing shown on the page. A wrong temp password or a new password that
            // fails the identity policy (AbpIdentity.Password.* settings — length, digit, uppercase,
            // non-alphanumeric) both come through here and need to render as a normal validation error.
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        // Mirrors TokenController.HandleChangePasswordAsync's own post-change step (the
        // /connect/token password-grant path this app doesn't use) — ChangePasswordAsync itself has
        // no idea this flag exists, so it never gets cleared on its own.
        user.SetShouldChangePasswordOnNextLogin(false);
        (await UserManager.UpdateAsync(user)).CheckErrors();

        await SignInManager.SignInAsync(user, isPersistent: false);

        return await RedirectSafelyAsync(ReturnUrl, ReturnUrlHash);
    }
}
