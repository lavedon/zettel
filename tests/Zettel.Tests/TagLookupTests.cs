using Zettel;

namespace Zettel.Tests;

[TestFixture]
public class TagLookupTests
{
    [Test]
    public void GetNotesByTag_Returns_Matching_Notes()
    {
        using var db = new Database(":memory:");
        db.InsertNote("/notes/a.md", "a.md", "Content A", "2024-01-01");
        var noteId = db.GetLastInsertedNoteId();
        var tagId = db.GetOrCreateTag("CodingCSharp");
        db.LinkNoteTag(noteId, tagId);

        db.InsertNote("/notes/b.md", "b.md", "Content B", "2024-01-01");
        var noteId2 = db.GetLastInsertedNoteId();
        db.LinkNoteTag(noteId2, tagId);

        var results = db.GetNotesByTag("CodingCSharp");

        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public void GetNotesByTag_Returns_Empty_For_Unknown_Tag()
    {
        using var db = new Database(":memory:");
        var results = db.GetNotesByTag("NonExistentTag");
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void GetAllTagsWithCounts_Returns_Sorted_By_Count()
    {
        using var db = new Database(":memory:");

        // Create notes and tags
        db.InsertNote("/a.md", "a.md", "A", "2024-01-01");
        var a = db.GetLastInsertedNoteId();
        db.InsertNote("/b.md", "b.md", "B", "2024-01-01");
        var b = db.GetLastInsertedNoteId();
        db.InsertNote("/c.md", "c.md", "C", "2024-01-01");
        var c = db.GetLastInsertedNoteId();

        var popular = db.GetOrCreateTag("Popular");
        var rare = db.GetOrCreateTag("Rare");

        db.LinkNoteTag(a, popular);
        db.LinkNoteTag(b, popular);
        db.LinkNoteTag(c, popular);
        db.LinkNoteTag(a, rare);

        var tags = db.GetAllTagsWithCounts();

        Assert.That(tags, Has.Count.EqualTo(2));
        Assert.That(tags[0].Name, Is.EqualTo("Popular"));
        Assert.That(tags[0].Count, Is.EqualTo(3));
        Assert.That(tags[1].Name, Is.EqualTo("Rare"));
        Assert.That(tags[1].Count, Is.EqualTo(1));
    }

    [Test]
    public void Deleting_Note_Cascades_To_NoteTag_Links()
    {
        using var db = new Database(":memory:");

        db.InsertNote("/a.md", "a.md", "Content", "2024-01-01");
        var noteId = db.GetLastInsertedNoteId();
        var tagId = db.GetOrCreateTag("TestTag");
        db.LinkNoteTag(noteId, tagId);

        Assert.That(db.GetNotesByTag("TestTag"), Has.Count.EqualTo(1));

        db.DeleteNoteByPath("/a.md");

        Assert.That(db.GetNotesByTag("TestTag"), Is.Empty);
    }

    [Test]
    public void Stats_Returns_Correct_Counts()
    {
        using var db = new Database(":memory:");

        db.InsertNote("/a.md", "a.md", "A", "2024-01-01");
        var noteId = db.GetLastInsertedNoteId();
        var tag1 = db.GetOrCreateTag("Tag1");
        var tag2 = db.GetOrCreateTag("Tag2");
        db.LinkNoteTag(noteId, tag1);
        db.LinkNoteTag(noteId, tag2);

        var stats = db.GetStats();
        Assert.That(stats.NoteCount, Is.EqualTo(1));
        Assert.That(stats.TagCount, Is.EqualTo(2));
        Assert.That(stats.LinkCount, Is.EqualTo(2));
    }
}
