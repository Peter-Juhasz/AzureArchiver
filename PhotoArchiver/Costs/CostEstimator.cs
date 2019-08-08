using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace PhotoArchiver.Costs
{
    using Storage;

    public class CostEstimator
    {
        public CostEstimator(IOptions<StorageOptions> storageOptions, IOptions<CostOptions> costOptions)
        {
            StorageOptions = storageOptions;
            CostOptions = costOptions.Value;
        }

        protected IOptions<StorageOptions> StorageOptions { get; }
        protected CostOptions CostOptions { get; }

        private const long GB = 1024 * 1024 * 1024;


        public int Reads { get; private set; } = 0;

        public void AddRead() => Reads++;


        public int Writes { get; private set; } = 0;

        public void AddWrite() => Writes++;

        public void AddWrite(long bytes)
        {
            AddWrite();
            AddBytes(bytes);
        }


        public int Others { get; private set; } = 0;

        public void AddOther() => Others++;


        public int ListOrCreateContainers { get; private set; } = 0;

        public void AddListOrCreateContainer() => ListOrCreateContainers++;


        public long Bytes { get; private set; } = 0;

        public void AddBytes(long bytes) => Bytes += bytes;


        public int KeyVaultOperations { get; private set; } = 0;

        public void AddKeyVaultOperation() => KeyVaultOperations++;


        public int DescribeTransactions { get; private set; } = 0;

        public void AddDescribe() => DescribeTransactions++;


        public int FaceTransactions { get; private set; } = 0;

        public void AddFace() => FaceTransactions++;


        public IEnumerable<(string item, long amount)> SummarizeUsage()
        {
            if (Bytes > 0)
            {
                yield return ("Bytes transferred", Bytes);
            }

            if (ListOrCreateContainers > 0)
            {
                yield return ("List or Create Container operations", ListOrCreateContainers);
            }

            if (KeyVaultOperations > 0)
            {
                yield return ("Key Vault transactions", KeyVaultOperations);
            }

            if (DescribeTransactions > 0)
            {
                yield return ("Computer Vision Describe transactions", DescribeTransactions);
            }

            if (FaceTransactions > 0)
            {
                yield return ("Face transactions", FaceTransactions);
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

        public IEnumerable<(string item, decimal cost)> SummarizeCosts()
        {
            if (Bytes > 0 && CostOptions.DataStoragePricePerGB != null)
            {
                yield return ("Data Storage (monthly)", (decimal)Bytes / GB * CostOptions.DataStoragePricePerGB.Value);
            }

            if (ListOrCreateContainers > 0 && CostOptions.ListOrCreateContainerPricePer10000 != null)
            {
                yield return ("List or Create Container operations (one time)", ListOrCreateContainers / 10000M * CostOptions.ListOrCreateContainerPricePer10000.Value);
            }

            if (KeyVaultOperations > 0 && CostOptions.KeyVaultTransactionPricePer10000 != null)
            {
                yield return ("Key Vault operations (one time)", KeyVaultOperations / 10000M * CostOptions.KeyVaultTransactionPricePer10000.Value);
            }

            if (DescribeTransactions > 0 && CostOptions.ComputerVisionDescribeTransactionPricePer1000 != null)
            {
                yield return ("Computer Vision Describe transactions", DescribeTransactions / 1000M * CostOptions.ComputerVisionDescribeTransactionPricePer1000.Value);
            }

            if (FaceTransactions > 0 && CostOptions.FaceTransactionPricePer1000 != null)
            {
                yield return ("Face transactions", FaceTransactions / 1000M * CostOptions.FaceTransactionPricePer1000.Value);
            }

            if (Reads > 0 && CostOptions.ReadPricePer10000 != null)
            {
                yield return ("Read operations (one time)", Reads / 10000M * CostOptions.ReadPricePer10000.Value);
            }

            if (Writes > 0 && CostOptions.WritePricePer10000 != null)
            {
                yield return ("Write operations (one time)", Writes / 10000M * CostOptions.WritePricePer10000.Value);
            }

            if (Others > 0 && CostOptions.OtherPricePer10000 != null)
            {
                yield return ("Other operations (one time)", Others / 10000M * CostOptions.OtherPricePer10000.Value);
            }

            if (Bytes > 0 && CostOptions.GRSDataTransferPricePerGB != null)
            {
                yield return ("Geo-Redundancy Data Transfer (one time)", (decimal)Bytes / GB * CostOptions.GRSDataTransferPricePerGB.Value);
            }
        }
    }
}
