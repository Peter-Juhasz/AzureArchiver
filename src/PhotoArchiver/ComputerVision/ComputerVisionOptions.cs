namespace PhotoArchiver.ComputerVision;

public class ComputerVisionOptions
{
	public Uri? Endpoint { get; set; }

	public string? Key { get; set; }


	public bool IsEnabled() => Key != null;
}
