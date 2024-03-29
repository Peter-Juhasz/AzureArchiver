﻿namespace PhotoArchiver;

using Files;

public class VerificationFailedException : Exception
{
	public VerificationFailedException(IFile info, Uri blob)
		: base($"Verification failed for file '{info.Name}'")
	{
		Info = info;
		Blob = blob;
	}

	public IFile Info { get; }

	public Uri Blob { get; }
}
