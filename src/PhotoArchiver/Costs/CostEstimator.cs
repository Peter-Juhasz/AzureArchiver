﻿using Microsoft.Extensions.Options;

namespace PhotoArchiver.Costs;

public class CostEstimator
{
	public CostEstimator(IOptions<CostOptions> costOptions)
	{
		CostOptions = costOptions.Value;
	}

	protected CostOptions CostOptions { get; }

	private const long GB = 1024 * 1024 * 1024;


	public int Reads { get; private set; }

	public void AddRead() => Reads++;

	public void AddRead(long bytes)
	{
		AddRead();
		AddBytesRead(bytes);
	}


	public int Writes { get; private set; }

	public void AddWrite() => Writes++;

	public void AddWrite(long bytes)
	{
		AddWrite();
		AddBytesWritten(bytes);
	}

	public int Others { get; private set; }

	public void AddOther() => Others++;


	public int ListOrCreateContainers { get; private set; }

	public void AddListOrCreateContainer() => ListOrCreateContainers++;


	public long BytesWritten { get; private set; }

	public void AddBytesWritten(long bytes) => BytesWritten += bytes;


	public int KeyVaultOperations { get; private set; }

	public void AddKeyVaultOperation() => KeyVaultOperations++;


	public int DescribeTransactions { get; private set; }

	public void AddDescribe() => DescribeTransactions++;


	public int FaceTransactions { get; private set; }

	public void AddFace() => FaceTransactions++;


	public long BytesRead { get; private set; }

	public void AddBytesRead(long bytes) => BytesRead += bytes;


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
