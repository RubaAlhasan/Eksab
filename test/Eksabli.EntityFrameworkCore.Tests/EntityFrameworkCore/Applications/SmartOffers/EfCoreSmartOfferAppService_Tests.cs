using Eksabli.SmartOffers;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.SmartOffers;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreSmartOfferAppService_Tests : SmartOfferAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
