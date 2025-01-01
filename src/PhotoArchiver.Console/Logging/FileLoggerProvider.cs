using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;

namespace PhotoArchiver.Logging;

internal class FileLoggerProvider : ILoggerProvider
{
	public FileLoggerProvider()
	{
		FileName = $"{DateTime.Now:yyyyMMddHHmmss}.txt";
	}

	private const string DirectoryName = "Logs";

	public string FileName { get; }


	public ILogger CreateLogger(string categoryName)
	{
		var fullPath = Path.Combine(DirectoryName, FileName);

		if (!Directory.Exists(DirectoryName))
		{
			Directory.CreateDirectory(DirectoryName);
		}

		return new FileLogger(fullPath);
	}

	public void Dispose() { }


	private sealed class FileLogger(string fileName) : ILogger
	{
		public string FileName { get; } = fileName;

		private static readonly Lock SyncRoot = new();

		private static readonly IDisposable Scope = new NullScope();


		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Scope;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			lock (SyncRoot)
			{
				File.AppendAllText(FileName, String.Join('\t', logLevel, eventId, formatter(state, exception)) + Environment.NewLine);
			}
		}


		private sealed class NullScope : IDisposable
		{
			public void Dispose() { }
		}
	}
}
