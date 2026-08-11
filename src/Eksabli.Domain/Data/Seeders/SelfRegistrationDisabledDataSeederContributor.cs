using System.Threading.Tasks;
using Volo.Abp.Account.Settings;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace Eksabli.Data.Seeders;

// Closes a real customer-identity gap, not just a settings tweak: stock Volo.Abp.Account's own
// self-registration endpoint (POST /api/account/register) was live, unmodified, and reachable —
// confirmed by reading every source file in this solution for an override or an explicit disable,
// finding neither. That endpoint creates a bare IdentityUser with zero knowledge of this app's own
// CustomerProfile concept (it's a stock ABP module, not Eksabli code), unlike the real customer
// registration path (OtpLoginService.ValidateAndResolveUserAsync), which always creates the
// IdentityUser and its CustomerProfile together, atomically. A user created via self-registration
// would show as "Unnamed customer" forever in the Business Portal's Customers list (no CustomerProfile
// row for FirstName/LastName to ever come from) unless they happened to separately call
// CustomerProfileAppService.UpdateMyProfileAsync, which nothing prompts them to do since it's not part
// of the self-registration flow at all — confirmed live: exactly this shape of orphaned account was
// found in the database this session (a manually-inserted test user, but the stock endpoint would
// reproduce the identical gap for a real one).
//
// Rather than teach the stock endpoint about CustomerProfile (it doesn't fit this app's actual
// identity model anyway — self-registration is username/password, this app's real customers are
// phone/OTP-identified, per the two-realm design), the correct fix is to close the gap at the source:
// disable self-registration entirely. OTP (already correct) is the only legitimate way a customer
// account gets created in this app; nothing else should be creating Host-realm users on a customer's
// behalf. CustomerProfileAppService.GetOrCreateAsync's own "defensive create-if-missing insurance"
// stays as a second layer for the update-profile path, but the real fix for "register" is here.
//
// Global (host-level) default — no TenantId, applies platform-wide. Idempotent: SetGlobalAsync just
// upserts the value, safe to run on every startup like every other contributor in this folder.
public class SelfRegistrationDisabledDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ISettingManager _settingManager;

    public SelfRegistrationDisabledDataSeederContributor(ISettingManager settingManager)
    {
        _settingManager = settingManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId != null)
        {
            return;
        }

        await _settingManager.SetGlobalAsync(AccountSettingNames.IsSelfRegistrationEnabled, "false");
    }
}
