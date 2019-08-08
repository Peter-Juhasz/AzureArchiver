using System;

namespace PhotoArchiver.KeyVault
{
    public class KeyVaultOptions
    {
        public Uri? KeyIdentifier { get; set; }

        public string? ClientId { get; set; }

        public string? ClientSecret { get; set; }

        public string? TenantId { get; set; }


        public bool IsEnabled() => KeyIdentifier != null;
    }
}
