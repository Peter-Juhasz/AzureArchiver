using Microsoft.Extensions.Options;

namespace PhotoArchiver.Costs;

public class CostEstimator(IOptions<CostOptions> costOptions)
{
	protected CostOptions CostOptions { get; } = costOptions.Value;

	private const long GB = 1024 * 1024 * 1024;


	private int _reads = 0;
	public int Reads => _reads;

	public void AddRead() => Interlocked.Increment(ref _reads);

	public void AddRead(long bytes)
	{
		AddRead();
		AddBytesRead(bytes);
	}


	private int _writes = 0;
	public int Writes => _writes;

	public void AddWrite() => Interlocked.Increment(ref _writes);

	public void AddWrite(long bytes)
	{
		AddWrite();
		AddBytesWritten(bytes);
	}

	private int _others = 0;
	public int Others => _others;

	public void AddOther() => Interlocked.Increment(ref _others);


	private int _listOrCreateContainers = 0;
	public int ListOrCreateContainers => _listOrCreateContainers;

	public void AddListOrCreateContainer() => Interlocked.Increment(ref _listOrCreateContainers);


	private long _bytesWritten = 0;
	public long BytesWritten => _bytesWritten;

	public void AddBytesWritten(long bytes) => Interlocked.Add(ref _bytesWritten, bytes);


	private int _keyVaultOperations = 0;
	public int KeyVaultOperations => _keyVaultOperations;

	public void AddKeyVaultOperation() => Interlocked.Increment(ref _keyVaultOperations);


	private int _describeTransactions = 0;
	public int DescribeTransactions => _describeTransactions;

	public void AddDescribe() => Interlocked.Increment(ref _describeTransactions);


	private int _faceTransactions = 0;
	public int FaceTransactions => _faceTransactions;

	public void AddFace() => Interlocked.Increment(ref _faceTransactions);


	private long _bytesRead = 0;
	public long BytesRead => _bytesRead;

	public void AddBytesRead(long bytes) => Interlocked.Add(ref _bytesRead, bytes);


	public IEnumerable<(string item, long amount)> SummarizeUsage()
	{
		if (BytesWritten > 0)
		{
			yield return ("Bytes transferred", BytesWritten);
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
		if (BytesWritten > 0 && CostOptions.DataStoragePricePerGB != null)
		{
			yield return ("Data Storage (monthly)", (decimal)BytesWritten / GB * CostOptions.DataStoragePricePerGB.Value);
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

		if (BytesRead > 0 && CostOptions.OutboundDataTransferPricePerGB != null)
		{
			yield return ("Outbound Data Transfer (one time)", (decimal)BytesRead / GB * CostOptions.OutboundDataTransferPricePerGB.Value);
		}

		if (BytesWritten > 0 && CostOptions.GRSDataTransferPricePerGB != null)
		{
			yield return ("Geo-Redundancy Data Transfer (one time)", (decimal)BytesWritten / GB * CostOptions.GRSDataTransferPricePerGB.Value);
		}
	}
}
