using System.Threading.Tasks;

namespace Eksabli.Data;

public interface IEksabliDbSchemaMigrator
{
    Task MigrateAsync();
}
