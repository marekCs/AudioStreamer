using Polly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Serilog;
using System.Threading;

namespace AudioStreamer
{
    public class StreamManager
    {
        private readonly string _playlistFolderPath;
        private readonly IFileOrganizer _fileOrganizer;
        private readonly List<Process> _ffmpegProcesses;
        private readonly CancellationToken _cancellationToken;

        public StreamManager(string playlistFolderPath, IFileOrganizer fileOrganizer, List<Process> ffmpegProcesses, CancellationToken cancellationToken)
        {
            _playlistFolderPath = playlistFolderPath;
            _fileOrganizer = fileOrganizer;
            _ffmpegProcesses = ffmpegProcesses;
            _cancellationToken = cancellationToken;
        }
        public async Task StartStreamingAsync(List<List<string>> streams, int basePort = 8083)
        {
            if (streams == null || streams.Count == 0)
            {
                Log.Error("No streams provided for streaming. Terminating the streaming process.");
                return;
            }

            Task[] streamTasks = new Task[streams.Count];

            for (int i = 0; i < streams.Count; i++)
            {
                var stream = streams[i];
                int port = basePort + i;

                streamTasks[i] = StartSingleStreamAsync(stream, i, port, _cancellationToken);
            }

            try
            {
                await Task.WhenAll(streamTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Streaming was cancelled.");
                Console.WriteLine("Streaming was cancelled.");
            }
            catch (AggregateException ae)
            {
                Log.Error($"One or more streams encountered errors: {ae.Message}");
                foreach (var innerEx in ae.InnerExceptions)
                {
                    Log.Error($"Inner Exception: {innerEx.Message}");
                }
            }
        }

        private async Task StartSingleStreamAsync(List<string> stream, int streamIndex, int port, CancellationToken cancellationToken)
        {
            string uniqueID = PlaylistGenerator.GenerateUniqueIdentifier(stream);

            if (cancellationToken.IsCancellationRequested)
            {
                Log.Information("Cancelation token requested, the program was terminated.");
                return;
            }

            if (IsStreamEmpty(stream, uniqueID))
                return;

            try
            {
                foreach (var audioFile in stream)
                {
                    await ProcessAudioFileAsync(audioFile, streamIndex, port, cancellationToken);
                }

                PostStreamActions(uniqueID);
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while streaming: {ex.Message}");
                throw new Exception($"An error occurred while streaming: {ex.Message}");
            }
        }

        private bool IsStreamEmpty(List<string> stream, string uniqueID)
        {
            if (stream == null || stream.Count == 0)
            {
                Log.Warning($"Stream {uniqueID} has no audio files. Skipping this stream.");
                Console.WriteLine($"Stream {uniqueID} has no audio files. Skipping this stream.");
                return true;
            }
            return false;
        }

        private async Task<string> PrepareAudioFileAsync(string audioFile, CancellationToken cancellationToken)
        {
            string processedFile = audioFile;

            if (!Path.GetExtension(audioFile).Equals(".aac", StringComparison.OrdinalIgnoreCase))
            {
                processedFile = Path.GetTempFileName() + ".aac";
                await ConvertToAACAsync(audioFile, processedFile, cancellationToken);
            }

            return processedFile;
        }

        private async Task ProcessAudioFileAsync(string audioFile, int streamIndex, int port, CancellationToken cancellationToken)
        {
            // string processedFile = await PrepareAudioFileAsync(audioFile, cancellationToken);
            // await StartFfmpegStreamingAsync(processedFile, streamIndex, port, cancellationToken);
            await StartFfmpegStreamingAsync(audioFile, streamIndex, port, cancellationToken);
        }

        private async Task StartFfmpegStreamingAsync(string processedFile, int streamIndex, int port, CancellationToken cancellationToken)
        {
            try
            {
                
                string serverAddress = "51.159.105.89";
                string sourceUsername = "source";
                string sourcePassword = "67VVd7;bo$Ie";

                var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    string m = $"FFmpeg failed. Waiting {timeSpan} before next retry. Retry attempt {retryCount}";
                    Log.Warning(m);
                    Console.WriteLine(m);
                });

                await retryPolicy.ExecuteAsync(async context =>  // Note the 'context' here
                {
                    string audioCodec, fileFormat, contentType;
                    if (processedFile.EndsWith(".aac"))
                    {
                        audioCodec = "libfdk_aac";
                        fileFormat = "adts";
                        contentType = "audio/aac";
                    }
                    else if (processedFile.EndsWith(".wma"))
                    {
                        audioCodec = "wmav2";
                        fileFormat = "asf";
                        contentType = "audio/x-ms-wma";
                    }
                    else // default to mp3 if the extension is not recognized
                    {
                        audioCodec = "libmp3lame";
                        fileFormat = "mp3";
                        contentType = "audio/mpeg";
                    }
                    // for Icecast
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-i {processedFile} -acodec {audioCodec} -ab 128k -ac 2 -content_type {contentType} -f {fileFormat} icecast://{sourceUsername}:{sourcePassword}@{serverAddress}:{port}/rai_{streamIndex + 1}",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        }
                    };

                    _ffmpegProcesses.Add(process);

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Log.Information($"FFmpeg Output: {e.Data}");
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            if (e.Data.Contains("Error") || e.Data.Contains("ERR"))
                            {
                                Log.Error($"FFmpeg Error: {e.Data}");
                            }
                        }
                    };

                    Log.Information($"Starting FFmpeg for streaming with arguments: {process.StartInfo.Arguments}");

                    if (process.Start())
                    {
                        Log.Information($"FFmpeg streaming process started with ID {process.Id}");
                        await Task.Run(() => process.WaitForExit(), cancellationToken);

                        Log.Information($"FFmpeg streaming process exited with code {process.ExitCode}");

                        if (process.ExitCode != 0)
                        {
                            Log.Error("FFmpeg streaming exited with an error code.");
                        }
                    }
                    else
                    {
                        Log.Error("Failed to start FFmpeg streaming process");
                    }

                    _ffmpegProcesses.Remove(process);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while executing FFmpeg: {ex.Message}");
                throw new Exception($"An error occurred while executing FFmpeg: {ex.Message}");
            }
        }

        private void PostStreamActions(string uniqueID)
        {
            Log.Information($"Stream {uniqueID} has ended");
            Console.WriteLine($"Stream {uniqueID} has ended");
            MoveToAlreadyStreamed(uniqueID);
        }

        private async Task ConvertToAACAsync(string inputFilePath, string outputFilePath, CancellationToken cancellationToken)
        {
            Log.Information($"Starting FFmpeg conversion from {inputFilePath} to {outputFilePath}");

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = $"-i {inputFilePath} -c:a aac {outputFilePath}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    };

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Log.Information($"FFmpeg Conversion Output: {e.Data}");
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            if (e.Data.Contains("Error") || e.Data.Contains("ERR"))
                            {
                                Log.Error($"FFmpeg Conversion Error: {e.Data}");
                            }
                        }
                    };

                    if (process.Start())
                    {
                        Log.Information($"FFmpeg conversion process started with ID {process.Id}");
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);

                        // Check if cancellation has been requested
                        cancellationToken.ThrowIfCancellationRequested();

                        if (process.ExitCode != 0)
                        {
                            Log.Error($"FFmpeg conversion process exited with code {process.ExitCode}");
                        }
                        else
                        {
                            Log.Information($"FFmpeg conversion process completed successfully.");
                        }
                    }
                    else
                    {
                        Log.Error("Failed to start FFmpeg conversion process");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Warning("FFmpeg conversion process was canceled");
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while converting file with FFmpeg: {ex.Message}");
            }

            Log.Information($"FFmpeg conversion from {inputFilePath} to {outputFilePath} completed.");
        }



        private void MoveToAlreadyStreamed(string uniqueID)
        {
            string sourcePath = Path.Combine(_playlistFolderPath, $"{uniqueID}.m3u8");
            string destPath = Path.Combine(_playlistFolderPath, "AlreadyStreamed", $"{uniqueID}.m3u8");

            if (System.IO.File.Exists(sourcePath))
            {
                System.IO.File.Move(sourcePath, destPath);
                _fileOrganizer.RemoveFromProcessedFiles(sourcePath);
            }
        }
    }
}
