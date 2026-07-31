using Microsoft.Extensions.Localization;
using Eksabli.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace Eksabli;

[Dependency(ReplaceServices = true)]
public class EksabliBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<EksabliResource> _localizer;

    public EksabliBrandingProvider(IStringLocalizer<EksabliResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
