using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace PhotoArchiver.Update;

public class GitHubUpdateService : IUpdateService
{
	public GitHubUpdateService(HttpClient http, IOptions<UpdateOptions> options, ILogger<GitHubUpdateService> logger)
	{
		Http = http;
		Options = options;
		Logger = logger;
	}

	protected HttpClient Http { get; }
	protected IOptions<UpdateOptions> Options { get; }
	protected ILogger<GitHubUpdateService> Logger { get; }

	private static readonly JsonSerializerOptions jsonSerializerOptions = new(JsonSerializerDefaults.Web);

	public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken)
	{
		var releases = await Http.GetFromJsonAsync<ReleaseDto[]>(Options.Value.Feed, jsonSerializerOptions);
		var latest = releases!.OrderByDescending(r => r.Name).First();

		var currentVersion = Version.Parse(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion!);
		var latestVersion = Version.Parse(latest.Name);

		return currentVersion < latestVersion;
	}


	private record class ReleaseDto(string Name);
}
