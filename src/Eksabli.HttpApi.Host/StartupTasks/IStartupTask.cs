using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Eksabli.StartupTasks;

public interface IStartupTask
{
    Task Execute(IHost host);
}
