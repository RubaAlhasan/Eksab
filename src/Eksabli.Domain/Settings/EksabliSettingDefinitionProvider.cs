using Eksabli.Billing;
using Volo.Abp.Settings;

namespace Eksabli.Settings;

public class EksabliSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new SettingDefinition(
                EksabliSettings.Trial.LengthDays,
                defaultValue: BillingConsts.TrialDurationDays.ToString(),
                isVisibleToClients: true),
            new SettingDefinition(
                EksabliSettings.MaintenanceMode,
                defaultValue: "false",
                isVisibleToClients: true),
            new SettingDefinition(
                EksabliSettings.Sms.ActiveProvider,
                defaultValue: "Null",
                isVisibleToClients: true));
    }
}
