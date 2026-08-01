using Eksabli.EmployeeAssignments;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.EmployeeAssignments;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreEmployeeAssignmentAppService_Tests : EmployeeAssignmentAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
