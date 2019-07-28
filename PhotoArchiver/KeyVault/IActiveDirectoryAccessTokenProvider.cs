using System.Threading.Tasks;

namespace PhotoArchiver.KeyVault
{
    public interface IActiveDirectoryAccessTokenProvider
    {
        Task<string> GetAccessTokenAsync(string resource);
    }
}