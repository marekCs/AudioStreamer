using Serilog;

namespace AudioStreamer
{
    public class PlaylistGenerator
    {
        private readonly string _audioFolderPath;
        private readonly string _playlistFolderPath;
        private readonly int _maxStreams;

        public PlaylistGenerator(string audioFolderPath, string playlistFolderPath, int maxStreams)
        {
            _audioFolderPath = audioFolderPath;
            _playlistFolderPath = playlistFolderPath;
            _maxStreams = maxStreams;

            if (!Directory.Exists(_playlistFolderPath))
            {
                Directory.CreateDirectory(_playlistFolderPath);
            }
        }

        public List<string> LoadAndSortAudioFiles()
        {
            var fileValidator = new FileOrganizer(_audioFolderPath);
            List<string> collectedFiles;

            try
            {
                collectedFiles = fileValidator.CollectFiles();
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while collecting the files: {ex.Message}");
                return new List<string>();
            }

            // Odfiltrování již vysílaných souborů
            collectedFiles = FilterAlreadyStreamedFiles(collectedFiles);

            if (collectedFiles.Count == 0)
            {
                Log.Warning("No valid (or new) audio files found in the specified date range.");
                Console.WriteLine("No valid (or new) audio files found in the specified date range.");
            }

            return collectedFiles;
        }

        private List<string> FilterAlreadyStreamedFiles(List<string> files)
        {
            var filteredFiles = new List<string>();

            foreach (var file in files)
            {
                string uniqueIdentifier = GenerateUniqueIdentifier(file);

                if (!IsAlreadyStreamed(uniqueIdentifier))
                {
                    filteredFiles.Add(file);
                }
                else
                {
                    Log.Error($"This playlist was already streamed: {uniqueIdentifier}.");
                    Console.WriteLine($"This playlist was already streamed: {uniqueIdentifier}.");
                }
            }

            return filteredFiles;
        }

        public List<List<string>> DivideIntoStreams(List<string> sortedFiles)
        {
            int totalFiles = sortedFiles.Count;
            int filesPerStream = (int)Math.Ceiling((double)totalFiles / _maxStreams);

            var streams = new List<List<string>>();

            for (int i = 0; i < _maxStreams; i++)
            {
                var startIdx = i * filesPerStream;
                var endIdx = Math.Min(startIdx + filesPerStream, totalFiles);

                if (startIdx >= totalFiles)
                {
                    break;
                }

                streams.Add(sortedFiles.GetRange(startIdx, endIdx - startIdx));
            }
            return streams;
        }

        public List<List<string>> GeneratePlaylists(List<List<string>> streams)
        {
            List<List<string>> streamsToStream = new List<List<string>>();
            foreach (var stream in streams)
            {
                string uniqueIdentifier = GenerateUniqueIdentifier(stream);
                string playlistName = $"{uniqueIdentifier}";

                if (!IsAlreadyStreamed(playlistName))
                {
                    streamsToStream.Add(stream);
                    GeneratePlaylist(stream, playlistName);
                }
            }
            Log.Information($"For a given amount of audio files, we will need a total of: {streamsToStream.Count} number of streams.");
            return streamsToStream;
        }


        public static string GenerateUniqueIdentifier(List<string> audioFiles)
        {
            return ParseFilePathToUniqueID(audioFiles[0], "Rai");
        }

        public static string GenerateUniqueIdentifier(string audioFile)
        {
            return ParseFilePathToUniqueID(audioFile, "Rai");
        }

        private void GeneratePlaylist(List<string> audioFiles, string playlistName)
        {
            string playlistPath = Path.Combine(_playlistFolderPath, $"{playlistName}.m3u8");
            using (StreamWriter writer = new StreamWriter(playlistPath))
            {
                writer.WriteLine("#EXTM3U");
                writer.WriteLine("#EXT-X-VERSION:3");
                writer.WriteLine("#EXT-X-MEDIA-SEQUENCE:0");
                writer.WriteLine("#EXT-X-ALLOW-CACHE:YES");

                foreach (var file in audioFiles)
                {
                    writer.WriteLine("#EXTINF:7200,");
                    writer.WriteLine(file);
                }
            }
        }

        private static string ParseFilePathToUniqueID(string filePath, string rootFolder)
        {
            int startIndex = filePath.IndexOf(rootFolder);
            if (startIndex != -1)
            {
                startIndex += rootFolder.Length + 1;
                string relativePath = filePath.Substring(startIndex);
                string[] pathParts = relativePath.Split(Path.DirectorySeparatorChar);

                string radioName = pathParts[0];
                string year = pathParts[1];
                string month = pathParts[2];
                string day = pathParts[3];
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string hour = fileName.Split('_')[2].Substring(0, 2);

                string uniqueID = $"{radioName}_{year}_{month}_{day}_{hour}";
                return uniqueID;
            }
            else
            {
                throw new Exception($"Folder {rootFolder} inside the audioPathFolder  wasn't found");
            }
        }

        private bool IsAlreadyStreamed(string uniqueIdentifier)
        {
            string path = Path.Combine(_playlistFolderPath, "AlreadyStreamed", $"{uniqueIdentifier}.m3u8");
            return File.Exists(path);
        }

        public List<string> GetPlaylists()
        {
            return Directory.GetFiles(_playlistFolderPath, "*.m3u8").ToList();
        }
    }
}
