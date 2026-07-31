using Eksabli.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Eksabli.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class EksabliController : AbpControllerBase
{
    protected EksabliController()
    {
        LocalizationResource = typeof(EksabliResource);
    }
}
