using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioStreamer.Utils
{
    public class AppConfig
    {
        public string? AudioFolderPath { get; set; }
        public string? PlaylistFolderPath { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int MaxStreams { get; set; }

        public static AppConfig Load(IConfiguration configuration)
        {
            // Initialize with default or empty values to satisfy non-nullable warning
            return new AppConfig
            {
                AudioFolderPath = configuration[Constants.AudioFolderPathKey] ?? string.Empty,
                PlaylistFolderPath = configuration[Constants.PlaylistFolderPathKey] ?? string.Empty,
                StartDate = DateTime.TryParse(configuration[Constants.StreamStartDateKey], out var startDate) ? startDate : DateTime.MinValue,
                EndDate = DateTime.TryParse(configuration[Constants.StreamEndDateKey], out var endDate) ? endDate : DateTime.MinValue,
                MaxStreams = int.TryParse(configuration[Constants.MaxStreamsKey], out var maxStreams) ? maxStreams : 20
            };
        }
    }
}
