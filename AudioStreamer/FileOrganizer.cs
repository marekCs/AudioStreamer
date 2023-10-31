using AudioStreamer.Utils;
using Serilog;
using System.Text.RegularExpressions;

namespace AudioStreamer
{
    public class FileOrganizer : IFileOrganizer
    {
        private readonly string _rootDirectory;
        // list of files that have already been added for streaming but have not yet been broadcast
        private HashSet<string> _processedFiles = new HashSet<string>();
        public List<string> InvalidFiles { get; } = new List<string>();

        public FileOrganizer(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
        }

        public bool ValidateFiles()
        {
            try
            {
                ProcessFiles(false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while validating the directory structure: {ex.Message}");
                return false;
            }
        }

        public List<string> CollectFiles()
        {
            var collectedFiles = new List<string>();
            try
            {
                ProcessFiles(true, collectedFiles);
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while collecting the files: {ex.Message}");
            }
            return collectedFiles;
        }


        private void ProcessFiles(bool collectFiles, List<string>? collectedFiles = null)
        {
            try
            {
                int fileCount = 0;

                // Validate root folder
                if (!Directory.Exists(_rootDirectory))
                {
                    Log.Error("Root directory does not exist.");
                    throw new InvalidOperationException("Root directory does not exist.");
                }

                // Iterate through all radio folders
                foreach (var radioDir in Directory.GetDirectories(_rootDirectory))
                {
                    var radioNamePattern = @"RAI\d+_(AAC|WMA)";
                    if (!Regex.IsMatch(Path.GetFileName(radioDir), radioNamePattern))
                    {
                        Log.Error($"Invalid radio folder name: {radioDir}");
                        continue;
                    }

                    // Iterate through all years
                    foreach (var yearDir in Directory.GetDirectories(radioDir))
                    {
                        // Validate year
                        if (!Regex.IsMatch(Path.GetFileName(yearDir), @"\d{4}"))
                        {
                            Log.Error($"Invalid year folder: {yearDir}");
                            continue;
                        }

                        // Iterate through all months
                        foreach (var monthDir in Directory.GetDirectories(yearDir))
                        {
                            // Validate month
                            if (!Regex.IsMatch(Path.GetFileName(monthDir), @"0[1-9]|1[0-2]"))
                            {
                                Log.Error($"Invalid month folder: {monthDir}");
                                continue;
                            }

                            // Iterate through all days
                            foreach (var dayDir in Directory.GetDirectories(monthDir))
                            {
                                // Validate day
                                if (!Regex.IsMatch(Path.GetFileName(dayDir), @"0[1-9]|[12][0-9]|3[01]"))
                                {
                                    Log.Error($"Invalid day folder: {dayDir}");
                                    continue;
                                }

                                // Iterate through all files in a day
                                foreach (var filePath in Directory.GetFiles(dayDir))
                                {
                                    // If this file has already been added, skip it.
                                    if (_processedFiles.Contains(filePath))
                                    {
                                        continue;
                                    }
                                    var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

                                    if (!Constants.SupportedAudioExtensions.Contains(fileExtension))
                                    {
                                        Log.Error($"Unsupported audio format: {filePath}");
                                        InvalidFiles.Add(filePath);
                                        continue;
                                    }

                                    var fileInfo = new FileInfo(filePath);
                                    if (fileInfo.Length <= 0)
                                    {
                                        Log.Error($"File is empty: {filePath}");
                                        continue;
                                    }

                                    // Add this file to the set of processed files
                                    _processedFiles.Add(filePath);

                                    fileCount++;

                                    // If we are collecting files, add them to the list.
                                    if (collectFiles && collectedFiles != null)
                                    {
                                        collectedFiles.Add(filePath);
                                    }

                                }
                            }
                        }
                    }
                }

                Log.Information($"Total valid files ready for streaming: {fileCount}");
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while validating the directory structure: {ex.Message}");
            }
        }

        public bool RemoveFromProcessedFiles(string filePath)
        {
            return _processedFiles.Remove(filePath);
        }
    }
}
