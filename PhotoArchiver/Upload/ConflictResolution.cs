namespace PhotoArchiver.Upload
{
    public enum ConflictResolution
    {
        Skip,
        KeepBoth,
        SnapshotAndOverwrite,
        Overwrite,
    }
}
