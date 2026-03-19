namespace Zettel;

public static class Indexer
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".html", ".htm"
    };

    public static IndexResult Index(string vaultPath, Database db)
    {
        var fullPath = Path.GetFullPath(vaultPath);
        var vaultFiles = ScanVault(fullPath);
        var dbTimestamps = db.GetAllNoteTimestamps();

        int added = 0, updated = 0, removed = 0, unchanged = 0;

        // Find new and modified files
        foreach (var (filepath, lastModified) in vaultFiles)
        {
            if (dbTimestamps.TryGetValue(filepath, out var dbTimestamp))
            {
                if (dbTimestamp == lastModified)
                {
                    unchanged++;
                    dbTimestamps.Remove(filepath);
                    continue;
                }

                // Modified — delete old, re-insert
                db.DeleteNoteByPath(filepath);
                IndexFile(filepath, lastModified, db);
                updated++;
                dbTimestamps.Remove(filepath);
            }
            else
            {
                // New file
                IndexFile(filepath, lastModified, db);
                added++;
            }
        }

        // Remaining entries in dbTimestamps are deleted files
        foreach (var filepath in dbTimestamps.Keys)
        {
            db.DeleteNoteByPath(filepath);
            removed++;
        }

        return new IndexResult(added, updated, removed, unchanged);
    }

    private static void IndexFile(string filepath, string lastModified, Database db)
    {
        var content = File.ReadAllText(filepath);
        var filename = Path.GetFileName(filepath);

        db.InsertNote(filepath, filename, content, lastModified);
        var noteId = db.GetLastInsertedNoteId();

        var tags = TagParser.Extract(content);
        foreach (var tag in tags)
        {
            var tagId = db.GetOrCreateTag(tag);
            db.LinkNoteTag(noteId, tagId);
        }
    }

    private static Dictionary<string, string> ScanVault(string vaultPath)
    {
        var files = new Dictionary<string, string>();

        foreach (var file in Directory.EnumerateFiles(vaultPath, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (!TextExtensions.Contains(ext))
                continue;

            // Skip hidden directories and files
            if (file.Contains(Path.DirectorySeparatorChar + ".") ||
                file.Contains(Path.AltDirectorySeparatorChar + "."))
                continue;

            var lastModified = File.GetLastWriteTimeUtc(file).ToString("O");
            files[Path.GetFullPath(file)] = lastModified;
        }

        return files;
    }
}

public record IndexResult(int Added, int Updated, int Removed, int Unchanged);
