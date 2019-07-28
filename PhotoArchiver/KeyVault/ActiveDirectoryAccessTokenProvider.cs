using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Threading.Tasks;

namespace PhotoArchiver.KeyVault
{
    public class ActiveDirectoryAccessTokenProvider : IActiveDirectoryAccessTokenProvider
    {
        public ActiveDirectoryAccessTokenProvider(IOptions<KeyVaultOptions> options, TokenCache tokenCache)
        {
            Options = options;
            TokenCache = tokenCache;
        }

        protected IOptions<KeyVaultOptions> Options { get; }
        protected TokenCache TokenCache { get; }

        public async Task<string> GetAccessTokenAsync(string resource)
        {
            var options = Options.Value;
            var clientCredential = new ClientCredential(
                options.ClientId,
                options.ClientSecret
            );
            var context = new AuthenticationContext($"https://login.windows.net/{options.TenantId}", TokenCache);
            var result = await context.AcquireTokenAsync(resource, clientCredential);
            return result.AccessToken;
        }
    }
}
