using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace PhotoArchiver.Update;

public class GitHubUpdateService(HttpClient http, IOptions<UpdateOptions> options, ILogger<GitHubUpdateService> logger) : IUpdateService
{
	private static readonly JsonSerializerOptions jsonSerializerOptions = new(JsonSerializerDefaults.Web);

	public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken)
	{
		var releases = await http.GetFromJsonAsync<ReleaseDto[]>(options.Value.Feed, jsonSerializerOptions, cancellationToken: cancellationToken);
		var latest = releases!.OrderByDescending(r => r.Name).First();

		var currentVersion = Version.Parse(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion!);
		var latestVersion = Version.Parse(latest.Name);
		logger.LogInformation("Current version: {CurrentVersion}, Latest version: {LatestVersion}", currentVersion, latestVersion);

		return currentVersion < latestVersion;
	}


	private record class ReleaseDto(string Name);
}
