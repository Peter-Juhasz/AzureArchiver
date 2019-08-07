using System.Linq;

namespace PhotoArchiver.Costs
{
    public class CostOptions
    {
        public string? Currency { get; set; } = "$";

        public decimal? ListOrCreateContainerPricePer10000 { get; set; }
        public decimal? ReadPricePer10000 { get; set; }
        public decimal? WritePricePer10000 { get; set; }
        public decimal? OtherPricePer10000 { get; set; }
        public decimal? DataStoragePricePerGB { get; set; }
        public decimal? GRSDataTransferPricePerGB { get; set; }

        public decimal? KeyVaultTransactionPricePer10000 { get; set; }

        public decimal? ComputerVisionDescribeTransactionPricePer1000 { get; set; }


        public bool IsAnySet() => typeof(CostOptions).GetProperties().Any(p => p.GetValue(this) != null);
    }
}
