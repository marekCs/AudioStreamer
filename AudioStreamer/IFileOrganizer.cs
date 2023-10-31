namespace AudioStreamer
{
    public interface IFileOrganizer
    {
        List<string> InvalidFiles { get; }

        List<string> CollectFiles();
        bool RemoveFromProcessedFiles(string filePath);
        bool ValidateFiles();
    }
}