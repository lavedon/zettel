using Zettel;

namespace Zettel.Tests;

[TestFixture]
public class SearchTests
{
    [Test]
    public void FTS_Search_Finds_Matching_Notes()
    {
        using var db = new Database(":memory:");
        db.InsertNote("/notes/a.md", "a.md", "SuperMemo incremental reading is powerful.", "2024-01-01");
        db.InsertNote("/notes/b.md", "b.md", "Algorithms and data structures.", "2024-01-01");

        var results = db.Search("supermemo");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Filepath, Is.EqualTo("/notes/a.md"));
    }

    [Test]
    public void FTS_Search_Returns_Empty_For_No_Match()
    {
        using var db = new Database(":memory:");
        db.InsertNote("/notes/a.md", "a.md", "Some content here.", "2024-01-01");

        var results = db.Search("nonexistent");

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void FTS_Search_Matches_Multiple_Notes()
    {
        using var db = new Database(":memory:");
        db.InsertNote("/notes/a.md", "a.md", "Learning C# programming.", "2024-01-01");
        db.InsertNote("/notes/b.md", "b.md", "Advanced C# programming.", "2024-01-01");
        db.InsertNote("/notes/c.md", "c.md", "Python basics.", "2024-01-01");

        var results = db.Search("programming");

        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public void FTS_Search_Returns_Snippet()
    {
        using var db = new Database(":memory:");
        db.InsertNote("/notes/a.md", "a.md", "The zettelkasten method is great for knowledge management.", "2024-01-01");

        var results = db.Search("zettelkasten");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Snippet, Does.Contain("zettelkasten"));
    }
}
