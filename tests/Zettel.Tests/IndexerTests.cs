using Zettel;

namespace Zettel.Tests;

[TestFixture]
public class IndexerTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zettel-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public void Indexes_Markdown_Files()
    {
        File.WriteAllText(Path.Combine(_tempDir, "note1.md"), "Hello #CodingCSharp world.");
        File.WriteAllText(Path.Combine(_tempDir, "note2.txt"), "Another #Algorithms note.");

        using var db = new Database(":memory:");
        var result = Indexer.Index(_tempDir, db);

        Assert.That(result.Added, Is.EqualTo(2));
        Assert.That(result.Updated, Is.EqualTo(0));
        Assert.That(result.Removed, Is.EqualTo(0));

        var stats = db.GetStats();
        Assert.That(stats.NoteCount, Is.EqualTo(2));
        Assert.That(stats.TagCount, Is.EqualTo(2));
    }

    [Test]
    public void Skips_Binary_Files()
    {
        File.WriteAllText(Path.Combine(_tempDir, "note.md"), "Hello #Test world.");
        File.WriteAllBytes(Path.Combine(_tempDir, "image.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        File.WriteAllText(Path.Combine(_tempDir, "doc.docx"), "not real docx");

        using var db = new Database(":memory:");
        var result = Indexer.Index(_tempDir, db);

        Assert.That(result.Added, Is.EqualTo(1));
    }

    [Test]
    public void Incremental_Sync_Detects_New_Files()
    {
        File.WriteAllText(Path.Combine(_tempDir, "note1.md"), "First note.");

        using var db = new Database(":memory:");
        var result1 = Indexer.Index(_tempDir, db);
        Assert.That(result1.Added, Is.EqualTo(1));

        // Add a second file
        File.WriteAllText(Path.Combine(_tempDir, "note2.md"), "Second note.");
        var result2 = Indexer.Index(_tempDir, db);

        Assert.That(result2.Added, Is.EqualTo(1));
        Assert.That(result2.Unchanged, Is.EqualTo(1));
    }

    [Test]
    public void Incremental_Sync_Detects_Deleted_Files()
    {
        var filePath = Path.Combine(_tempDir, "note1.md");
        File.WriteAllText(filePath, "Will be deleted.");

        using var db = new Database(":memory:");
        Indexer.Index(_tempDir, db);

        File.Delete(filePath);
        var result = Indexer.Index(_tempDir, db);

        Assert.That(result.Removed, Is.EqualTo(1));
        Assert.That(db.GetStats().NoteCount, Is.EqualTo(0));
    }

    [Test]
    public void Incremental_Sync_Detects_Modified_Files()
    {
        var filePath = Path.Combine(_tempDir, "note1.md");
        File.WriteAllText(filePath, "Original content #OldTag.");

        using var db = new Database(":memory:");
        Indexer.Index(_tempDir, db);

        // Modify the file — need to ensure timestamp changes
        System.Threading.Thread.Sleep(100);
        File.WriteAllText(filePath, "Updated content #NewTag.");
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddSeconds(1));

        var result = Indexer.Index(_tempDir, db);

        Assert.That(result.Updated, Is.EqualTo(1));
        Assert.That(result.Unchanged, Is.EqualTo(0));
    }

    [Test]
    public void Indexes_Subdirectories()
    {
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "deep.md"), "Deep note #DeepTag.");

        using var db = new Database(":memory:");
        var result = Indexer.Index(_tempDir, db);

        Assert.That(result.Added, Is.EqualTo(1));
        Assert.That(db.GetNotesByTag("DeepTag"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Extracts_Tags_Into_Database()
    {
        File.WriteAllText(Path.Combine(_tempDir, "tagged.md"),
            "This has #CodingCSharp and #Algorithms tags.");

        using var db = new Database(":memory:");
        Indexer.Index(_tempDir, db);

        var csharpNotes = db.GetNotesByTag("CodingCSharp");
        var algoNotes = db.GetNotesByTag("Algorithms");

        Assert.That(csharpNotes, Has.Count.EqualTo(1));
        Assert.That(algoNotes, Has.Count.EqualTo(1));
    }
}
