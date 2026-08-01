using System.Threading.Tasks;

namespace Eksabli.Sms;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message);
}
