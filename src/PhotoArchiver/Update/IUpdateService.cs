namespace PhotoArchiver.Update;

public interface IUpdateService
{
	Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken);
}
