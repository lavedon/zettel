using Zettel;

namespace Zettel.Tests;

[TestFixture]
public class TagParserTests
{
    [Test]
    public void Extracts_HashTags()
    {
        var content = "This is about #CodingCSharp and #Algorithms in general.";
        var tags = TagParser.Extract(content);

        Assert.That(tags, Does.Contain("CodingCSharp"));
        Assert.That(tags, Does.Contain("Algorithms"));
    }

    [Test]
    public void Extracts_DollarTags()
    {
        var content = "See also $CodingPowerShellJobs and $CodingJavaScriptAsync for more.";
        var tags = TagParser.Extract(content);

        Assert.That(tags, Does.Contain("CodingPowerShellJobs"));
        Assert.That(tags, Does.Contain("CodingJavaScriptAsync"));
    }

    [Test]
    public void Ignores_SuperMemo_Metadata()
    {
        var content = """
            #SuperMemo Reference:
            #Title: Some document title
            #Author: Udemy Instructor
            #Link: https://example.com
            #Source: My Source.md
            #Parent: 1041: Writing
            """;
        var tags = TagParser.Extract(content);

        Assert.That(tags, Is.Empty);
    }

    [Test]
    public void Ignores_SingleCharacter_Tags()
    {
        var content = "Check #i and #r and #s values.";
        var tags = TagParser.Extract(content);

        Assert.That(tags, Is.Empty);
    }

    [Test]
    public void Ignores_Numeric_Tags_With_Letter_Prefix()
    {
        // Tags like #a123 where it's just a letter + digits
        var content = "See item #a123 for details.";
        var tags = TagParser.Extract(content);

        Assert.That(tags, Is.Empty);
    }

    [Test]
    public void Extracts_Tags_With_Hyphens()
    {
        var content = "The topic of #Anarcho-Capitalism is complex.";
        var tags = TagParser.Extract(content);

        Assert.That(tags, Does.Contain("Anarcho-Capitalism"));
    }

    [Test]
    public void Extracts_Tag_At_Start_Of_Line()
    {
        var content = "#CodingCSharp\nSome content here.";
        var tags = TagParser.Extract(content);

        Assert.That(tags, Does.Contain("CodingCSharp"));
    }

    [Test]
    public void Does_Not_Duplicate_Tags()
    {
        var content = "#CodingCSharp is great. I love #CodingCSharp so much.";
        var tags = TagParser.Extract(content);

        Assert.That(tags.Count, Is.EqualTo(1));
        Assert.That(tags, Does.Contain("CodingCSharp"));
    }

    [Test]
    public void Mixed_Hash_And_Dollar_Tags()
    {
        var content = "#CodingCSharp and $CodingAssembly are different.";
        var tags = TagParser.Extract(content);

        Assert.That(tags, Does.Contain("CodingCSharp"));
        Assert.That(tags, Does.Contain("CodingAssembly"));
    }

    [Test]
    public void Empty_Content_Returns_Empty()
    {
        var tags = TagParser.Extract("");
        Assert.That(tags, Is.Empty);
    }
}
