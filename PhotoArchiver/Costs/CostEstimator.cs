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

        // https://azure.microsoft.com/en-us/pricing/details/storage/blobs/
        // based on West US 2
        private const decimal ListOrCreateContainerPrice = 0.05M / 10000;
        private const decimal CoolReadPrice = 0.01M / 10000;
        private const decimal WritePrice = 0.10M / 10000;
        private const decimal OtherPrice = 0.004M / 10000;
        private const decimal LRSCoolPricePerGB = 0.01M;
        private const decimal LRSArchivePricePerGB = 0.00099M;
        private const decimal GRSDataTransferPricePerGB = 0.02M;

        private const decimal KeyVaultOperationPrice = 0.03M / 10000;

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


        public IEnumerable<(string item, long amount)> SummarizeUsage()
        {
            if (Bytes > 0)
            {
                yield return ("Bytes transferred", Bytes);
            }

            if (ListOrCreateContainerPrice > 0)
            {
                yield return ("List or Create Container operations", ListOrCreateContainers);
            }

            if (KeyVaultOperations > 0)
            {
                yield return ("Key Vault transactions", KeyVaultOperations);
            }

            if (Reads > 0)
            {
                yield return ("Read operations", Reads);
            }

            if (Writes > 0)
            {
                yield return ("Write operations", Writes);
            }

            if (Others > 0)
            {
                yield return ("Other operations", Others);
            }
        }

        public IEnumerable<(string item, decimal cost)> Summarize()
        {
            if (Bytes > 0)
            {
                yield return ("Data Storage (monthly)", (decimal)Bytes / GB * (StorageOptions.Value.Archive ? LRSArchivePricePerGB : LRSCoolPricePerGB));
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
                yield return ("Read operations (one time)", Reads * CoolReadPrice);
            }

            if (Writes > 0)
            {
                yield return ("Write operations (one time)", Writes * WritePrice);
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
