using Eksabli.Localization;
using Volo.Abp.Application.Services;

namespace Eksabli;

/* Inherit your application services from this class.
 */
public abstract class EksabliAppService : ApplicationService
{
    protected EksabliAppService()
    {
        LocalizationResource = typeof(EksabliResource);
    }
}
