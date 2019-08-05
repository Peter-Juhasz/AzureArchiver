using System.Threading.Tasks;

namespace PhotoArchiver.Update
{
    public interface IUpdateService
    {
        Task<bool> CheckForUpdatesAsync();
    }
}