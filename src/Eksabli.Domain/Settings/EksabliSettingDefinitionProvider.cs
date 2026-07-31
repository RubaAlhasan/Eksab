using Volo.Abp.Settings;

namespace Eksabli.Settings;

public class EksabliSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(EksabliSettings.MySetting1));
    }
}
