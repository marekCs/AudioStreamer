namespace AudioStreamer.Utils
{
    public static class Constants
    {
        public const string AudioFolderPathKey = "AudioStreamerConfig:AudioFolderPath";
        public const string PlaylistFolderPathKey = "AudioStreamerConfig:PlaylistFolderPath";
        public const string StreamStartDateKey = "AudioStreamerConfig:StreamStartDate";
        public const string StreamEndDateKey = "AudioStreamerConfig:StreamEndDate";
        public const string MaxStreamsKey = "AudioStreamerConfig:MaxStreams";

        public static readonly HashSet<string> SupportedAudioExtensions = new()
        {
            ".aac", ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".wma" // add more as needed
        };
    }
}
