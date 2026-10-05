using Eksabli.Data.Seeders;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Data;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreSmartOfferPermissionBackfill_Tests : SmartOfferPermissionBackfill_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
