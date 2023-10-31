using AudioStreamer;
using AudioStreamer.Utils;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

class Program
{
    // Define the ffmpegProcesses list here as a static field so it can be accessed in static methods
    private static List<Process> _ffmpegProcesses = new List<Process>();
    static async Task Main(string[] args)
    {
        if (!IsIcecastRunning("localhost", 8200))
        {
            Console.WriteLine("Icecast is not running. Please start the service.");
            return;
        }

        var cts = new CancellationTokenSource();

        // Register the event to capture a user break, like control-c
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            cts.Cancel();
            eventArgs.Cancel = true;  // cancel the break to allow the program to shutdown cleanly
        };

        try
        {
            IConfiguration configuration = InitializeConfiguration();
            Log.Logger = new LoggerConfiguration()
                            .ReadFrom.Configuration(configuration)
                            .CreateLogger();

            AppConfig appConfig = AppConfig.Load(configuration);

            IFileOrganizer fileOrganizer = new FileOrganizer(appConfig.AudioFolderPath);
            if (!fileOrganizer.ValidateFiles())
            {
                Log.Error("File validation failed. Terminating application.");
                if (fileOrganizer.InvalidFiles.Count > 0)
                {
                    Console.WriteLine("List of invalid files:");
                    foreach (var invalidFile in fileOrganizer.InvalidFiles)
                    {
                        Console.WriteLine(invalidFile);
                    }
                }
                Environment.Exit(1);
            }

            await StartStreaming(
            fileOrganizer,
            appConfig.AudioFolderPath,
            appConfig.PlaylistFolderPath,
            appConfig.MaxStreams,
            _ffmpegProcesses,
                cts.Token
        );
        }
        catch (OperationCanceledException)
        {
            Log.Information("Operation was cancelled, stopping streams.");
        }
        catch (Exception ex)
        {
            Log.Fatal($"An unhandled exception occurred: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static IConfiguration InitializeConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }

    static void ValidateAndInitializeSettings(IConfiguration configuration, out string audioFolderPath, out string playlistFolderPath, out DateTime startDate, out DateTime endDate, out int? maxStreams)
    {
        string? audioFolderPathTemp = configuration[Constants.AudioFolderPathKey];
        string? playlistFolderPathTemp = configuration[Constants.PlaylistFolderPathKey];
        string? startDateString = configuration[Constants.StreamStartDateKey];
        string? endDateString = configuration[Constants.StreamEndDateKey];
        string? maxStreamsString = configuration[Constants.MaxStreamsKey];

        int parsedMaxStreams = 0;

        if (audioFolderPathTemp == null ||
            playlistFolderPathTemp == null ||
            startDateString == null ||
            endDateString == null ||
            maxStreamsString == null ||
            !int.TryParse(maxStreamsString, out parsedMaxStreams))
        {
            Log.Error("Configuration is missing required values or they are incorrect. Terminating application.");
            Environment.Exit(1);
        }

        if (!Validator.ValidateAudioFolderPath(audioFolderPathTemp) ||
            !Validator.ValidatePlaylistFolderPath(playlistFolderPathTemp) ||
            !Validator.ValidateDateTime(startDateString, endDateString) ||
            !Validator.ValidateMaxStreams(maxStreamsString))
        {
            Log.Error("Invalid configuration. Terminating application.");
            Environment.Exit(1);
        }

        audioFolderPath = audioFolderPathTemp;
        playlistFolderPath = playlistFolderPathTemp;
        startDate = DateTime.Parse(startDateString);
        endDate = DateTime.Parse(endDateString);
        maxStreams = parsedMaxStreams;
    }

    static async Task WaitForStreamingToStart(DateTime startDate, DateTime endDate)
    {
        DateTime currentTime = DateTime.Now;

        if (currentTime > endDate)
        {
            Log.Error("Current date is beyond the end date for streaming. Terminating application.");
            Environment.Exit(1);
        }

        TimeSpan delay = startDate - currentTime;
        if (delay > TimeSpan.Zero)
        {
            Console.WriteLine($"Waiting until {startDate:dd/MM/yyyy HH:mm:ss} to start streaming.");
            await Task.Delay(delay);
        }

        Console.WriteLine($"Start streaming, but don't stream files beyond {endDate:dd/MM/yyyy HH:mm:ss}.");
        Log.Information("Start streaming...");
    }

    static async Task StartStreaming(IFileOrganizer fileOrganizer, string audioFolderPath, string playlistFolderPath, int maxStreams, List<Process> ffmpegProcesses,
        CancellationToken cancellationToken)
    {
        PlaylistGenerator playlistGenerator = new PlaylistGenerator(audioFolderPath, playlistFolderPath, maxStreams);

        // Load audio files
        List<string> sortedFiles = playlistGenerator.LoadAndSortAudioFiles();

        // Separate them into streams
        List<List<string>> streams = playlistGenerator.DivideIntoStreams(sortedFiles);

        // Generate playlists
        playlistGenerator.GeneratePlaylists(streams);

        StreamManager streamManager = new StreamManager(playlistFolderPath, fileOrganizer, ffmpegProcesses,
            cancellationToken);

        // Make streams
        await streamManager.StartStreamingAsync(streams);
    }

    static bool IsIcecastRunning(string hostname, int port)
    {
        using (TcpClient tcpClient = new TcpClient())
        {
            try
            {
                tcpClient.Connect(hostname, port);
                return true; // Spojení bylo úspěšné, Icecast pravděpodobně běží
            }
            catch (Exception)
            {
                return false; // Nepodařilo se navázat spojení, Icecast pravděpodobně neběží
            }
        }
    }
}
