using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Account;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.Authorization;
using Volo.Abp.Users;

namespace Eksabli.Filters;

// Server-side counterpart to the Angular mustChangePasswordGuard (angular/src/app/core/guards) — that
// guard only stops in-app navigation, so a caller hitting the API directly with a valid temp-password
// token was never actually blocked. Registered globally on MvcOptions.Filters in EksabliHttpApiHostModule,
// so it runs on every conventional/explicit API controller action (Host, Business, and Customer realms
// alike) for whichever user is currently authenticated.
public class MustChangePasswordFilter : IAsyncActionFilter
{
    // Endpoints a user who still must change their password needs to keep working: changing the password
    // itself (ABP's built-in account module) and reading their own must-change-password status (so the
    // SPA guard/toast has something to show). Everything else is blocked while the flag is set.
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/account/my-profile/change-password",
        "/api/app/account-status/must-change-password",
    };

    private readonly ICurrentUser _currentUser;
    private readonly IAccountStatusAppService _accountStatusAppService;

    public MustChangePasswordFilter(ICurrentUser currentUser, IAccountStatusAppService accountStatusAppService)
    {
        _currentUser = currentUser;
        _accountStatusAppService = accountStatusAppService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_currentUser.IsAuthenticated)
        {
            await next();
            return;
        }

        var path = context.HttpContext.Request.Path.Value?.TrimEnd('/');
        if (path != null && AllowedPaths.Contains(path))
        {
            await next();
            return;
        }

        if (await _accountStatusAppService.GetMustChangePasswordAsync())
        {
            // ABP's own exception-handling pipeline maps this to 403 Forbidden with the app's standard
            // error envelope — same mechanism [Authorize(SomePermission)] failures already go through.
            throw new AbpAuthorizationException("You must change your password before continuing.");
        }

        await next();
    }
}
