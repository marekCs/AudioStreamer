using Serilog;
using System.Globalization;

namespace AudioStreamer.Utils
{
    public static class Validator
    {
        public static bool ValidateAudioFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Log.Error("Audio folder path is null or empty.");
                return false;
            }
            return true;
        }

        public static bool ValidatePlaylistFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Log.Error("Playlist folder path is null or empty.");
                return false;
            }

            if (!Directory.Exists(path))
            {
                Log.Warning($"Playlist folder does not exist: {path}. Creating now.");
                try
                {
                    Directory.CreateDirectory(path);
                    Log.Information($"Successfully created playlist folder at {path}.");
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not create playlist folder. Error: {ex.Message}");
                    return false;
                }
            }

            return true;
        }


        public static bool ValidateDateTime(string startDate, string endDate)
        {
            try
            {
                if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStartDate))
                {
                    Log.Error($"Invalid start date format: {startDate}");
                    return false;
                }

                if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedEndDate))
                {
                    Log.Error($"Invalid end date format: {endDate}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while validating date formats: {ex.Message}");
                return false;
            }

            return true;
        }



        public static bool ValidateMaxStreams(string maxStreams)
        {
            if (!int.TryParse(maxStreams, out int parsedValue))
            {
                Log.Error("Max streams is not a valid integer.");
                return false;
            }

            if (parsedValue <= 0)
            {
                Log.Error("Max streams must be greater than zero.");
                return false;
            }

            return true;
        }
    }
}
