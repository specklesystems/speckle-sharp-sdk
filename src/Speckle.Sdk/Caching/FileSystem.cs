using Speckle.InterfaceGenerator;

namespace Speckle.Sdk.Caching;

[GenerateAutoInterface]
public class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public IEnumerable<string> EnumerateFiles(string path) => Directory.EnumerateFiles(path);

    public void DeleteFile(string path) => File.Delete(path);

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public string Combine(params string[] paths) => Path.Combine(paths);
}
