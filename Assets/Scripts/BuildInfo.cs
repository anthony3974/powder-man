namespace PowderMan
{
    /// <summary>
    /// The game's version. Bump both for every release: Android only installs an update when VersionCode goes up.
    /// The Android build script copies these into Player Settings, and the home menu shows them.
    /// </summary>
    public static class BuildInfo
    {
        public const string Version = "1.7";
        public const int VersionCode = 8;
    }
}
