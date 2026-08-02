using Eksabli.Campaigns;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Campaigns;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreCampaignAppService_Tests : CampaignAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
