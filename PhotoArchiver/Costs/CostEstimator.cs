using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace PhotoArchiver.Costs
{
    public class CostEstimator
    {
        public CostEstimator(IOptions<StorageOptions> storageOptions)
        {
            StorageOptions = storageOptions;
        }

        protected IOptions<StorageOptions> StorageOptions { get; }

        private const decimal ListOrCreateContainerPrice = 0.0422M / 10000;
        private const decimal ReadPrice = 0.0422M / 10000;
        private const decimal WritePrice = 0.0844M / 10000;
        private const decimal OtherPrice = 0.0034M / 10000;
        private const decimal CoolPricePerGB = 0.00844M;
        private const decimal ArchivePricePerGB = 0.00084M;
        private const decimal GRSDataTransferPricePerGB = 0.0169M;

        private const decimal KeyVaultOperationPrice = 0.026M / 10000;

        private const long GB = 1024 * 1024 * 1024;


        public int Reads { get; private set; } = 0;

        public void AddRead() => Reads++;


        public int Writes { get; private set; } = 0;

        public void AddWrite() => Writes++;


        public int Others { get; private set; } = 0;

        public void AddOther() => Others++;


        public int ListOrCreateContainers { get; private set; } = 0;

        public void AddListOrCreateContainer() => ListOrCreateContainers++;


        public long Bytes { get; private set; } = 0;

        public void AddBytes(long bytes) => Bytes += bytes;


        public int KeyVaultOperations { get; private set; } = 0;

        public void AddKeyVaultOperation() => KeyVaultOperations++;


        public IEnumerable<(string, decimal)> Summarize()
        {
            if (Bytes > 0)
            {
                yield return ("Data Storage (monthly)", (decimal)Bytes / GB * (StorageOptions.Value.Archive ? ArchivePricePerGB : CoolPricePerGB));
            }

            if (ListOrCreateContainerPrice > 0)
            {
                yield return ("List or Create Container operations (one time)", ListOrCreateContainers * ListOrCreateContainerPrice);
            }

            if (KeyVaultOperations > 0)
            {
                yield return ("Key Vault operations (one time)", KeyVaultOperations * KeyVaultOperationPrice);
            }

            if (Reads > 0)
            {
                yield return ("Read operations (one time)", Reads * ReadPrice);
            }

            if (Writes > 0)
            {
                yield return ("Writes operations (one time)", Writes * WritePrice);
            }

            if (Others > 0)
            {
                yield return ("Other operations (one time)", Others * OtherPrice);
            }

            if (Bytes > 0)
            {
                yield return ("Geo-Redundancy Data Transfer (one time)", (decimal)Bytes / GB * GRSDataTransferPricePerGB);
            }
        }
    }
}
