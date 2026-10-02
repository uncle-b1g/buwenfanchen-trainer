using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace FanchenTrainer;

internal static class SaveBackup
{
    // This snapshot preserves existing on-disk saves. It deliberately does not force a game save.
    public static string Create(string source, string destinationRoot, string session)
    {
        source = Path.GetFullPath(source);
        destinationRoot = Path.GetFullPath(destinationRoot);
        if (!Directory.Exists(source)) throw new IOException("没有找到存档目录；请先在游戏中存档，再执行修改。");
        if (destinationRoot.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("备份目录不能位于存档目录内。");
        var files = new List<string>();
        Collect(source, files);
        if (files.Count == 0) throw new IOException("存档目录为空；请先在游戏中存档。");
        Directory.CreateDirectory(destinationRoot);
        var path = Path.Combine(destinationRoot, $"before-edit-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.zip");
        var partial = path + ".partial";
        var locked = new Dictionary<string, FileStream>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Hold every file open for the full snapshot, preventing a mixed save generation.
            foreach (var file in files)
                locked.Add(file, new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            var verifiedFiles = new List<string>();
            Collect(source, verifiedFiles);
            if (!files.OrderBy(x => x).SequenceEqual(verifiedFiles.OrderBy(x => x)))
                throw new IOException("存档文件列表发生变化，请稍后重试。");
            var manifest = new List<object>();
            using (var archive = ZipFile.Open(partial, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    var stamp = info.LastWriteTimeUtc;
                    var length = info.Length;
                    var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                    // Deny writers during each read; an active asynchronous save aborts this backup.
                    var input = locked[file];
                    var entry = archive.CreateEntry("StorageV1/" + relative, CompressionLevel.Optimal);
                    string hash;
                    using (var output = entry.Open())
                    using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                    {
                        var buffer = new byte[81920];
                        int count;
                        while ((count = input.Read(buffer)) > 0)
                        {
                            sha.AppendData(buffer, 0, count);
                            output.Write(buffer, 0, count);
                        }
                        hash = Convert.ToHexString(sha.GetHashAndReset());
                    }
                    info.Refresh();
                    if (info.Length != length || info.LastWriteTimeUtc != stamp)
                        throw new IOException("存档正在写入，备份已中止；请稍后重试。");
                    manifest.Add(new { file = relative, bytes = length, sha256 = hash });
                }
                verifiedFiles.Clear();
                Collect(source, verifiedFiles);
                if (!files.OrderBy(x => x).SequenceEqual(verifiedFiles.OrderBy(x => x)))
                    throw new IOException("备份过程中存档文件列表发生变化，请稍后重试。");
                using var writer = new StreamWriter(archive.CreateEntry("backup-info.json").Open());
                writer.Write(JsonSerializer.Serialize(new { createdUtc = DateTime.UtcNow, session, source, files = manifest },
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            File.Move(partial, path);
            return path;
        }
        catch
        {
            if (File.Exists(partial)) File.Delete(partial);
            throw;
        }
        finally
        {
            foreach (var input in locked.Values) input.Dispose();
        }
    }

    private static void Collect(string directory, List<string> files)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("存档目录包含链接，无法确认备份范围。");
        foreach (var file in Directory.GetFiles(directory))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("存档包含链接，无法确认备份范围。");
            files.Add(file);
        }
        foreach (var sub in Directory.GetDirectories(directory)) Collect(sub, files);
    }
}
