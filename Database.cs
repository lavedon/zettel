using Microsoft.Data.Sqlite;

namespace Zettel;

public class Database : IDisposable
{
    private readonly SqliteConnection _conn;

    public Database(string dbPath)
    {
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS notes (
                id INTEGER PRIMARY KEY,
                filepath TEXT NOT NULL UNIQUE,
                filename TEXT NOT NULL,
                content TEXT NOT NULL,
                last_modified TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS tags (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS note_tags (
                note_id INTEGER REFERENCES notes(id) ON DELETE CASCADE,
                tag_id INTEGER REFERENCES tags(id) ON DELETE CASCADE,
                PRIMARY KEY (note_id, tag_id)
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS notes_fts USING fts5(
                filepath, filename, content,
                content=notes, content_rowid=id
            );

            CREATE TRIGGER IF NOT EXISTS notes_ai AFTER INSERT ON notes BEGIN
                INSERT INTO notes_fts(rowid, filepath, filename, content)
                VALUES (new.id, new.filepath, new.filename, new.content);
            END;

            CREATE TRIGGER IF NOT EXISTS notes_ad AFTER DELETE ON notes BEGIN
                INSERT INTO notes_fts(notes_fts, rowid, filepath, filename, content)
                VALUES ('delete', old.id, old.filepath, old.filename, old.content);
            END;

            CREATE TRIGGER IF NOT EXISTS notes_au AFTER UPDATE ON notes BEGIN
                INSERT INTO notes_fts(notes_fts, rowid, filepath, filename, content)
                VALUES ('delete', old.id, old.filepath, old.filename, old.content);
                INSERT INTO notes_fts(rowid, filepath, filename, content)
                VALUES (new.id, new.filepath, new.filename, new.content);
            END;
            """;
        cmd.ExecuteNonQuery();
    }

    public void InsertNote(string filepath, string filename, string content, string lastModified)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO notes (filepath, filename, content, last_modified)
            VALUES (@filepath, @filename, @content, @lastModified)
            """;
        cmd.Parameters.AddWithValue("@filepath", filepath);
        cmd.Parameters.AddWithValue("@filename", filename);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@lastModified", lastModified);
        cmd.ExecuteNonQuery();
    }

    public long GetLastInsertedNoteId()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return (long)cmd.ExecuteScalar()!;
    }

    public long GetOrCreateTag(string tagName)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO tags (name) VALUES (@name)";
        cmd.Parameters.AddWithValue("@name", tagName);
        cmd.ExecuteNonQuery();

        using var selectCmd = _conn.CreateCommand();
        selectCmd.CommandText = "SELECT id FROM tags WHERE name = @name";
        selectCmd.Parameters.AddWithValue("@name", tagName);
        return (long)selectCmd.ExecuteScalar()!;
    }

    public void LinkNoteTag(long noteId, long tagId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO note_tags (note_id, tag_id) VALUES (@noteId, @tagId)";
        cmd.Parameters.AddWithValue("@noteId", noteId);
        cmd.Parameters.AddWithValue("@tagId", tagId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteNoteByPath(string filepath)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM notes WHERE filepath = @filepath";
        cmd.Parameters.AddWithValue("@filepath", filepath);
        cmd.ExecuteNonQuery();
    }

    public Dictionary<string, string> GetAllNoteTimestamps()
    {
        var result = new Dictionary<string, string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT filepath, last_modified FROM notes";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }
        return result;
    }

    public List<SearchResult> Search(string query)
    {
        var results = new List<SearchResult>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.filepath, n.filename, snippet(notes_fts, 2, '>>>', '<<<', '...', 32) as snippet
            FROM notes_fts
            JOIN notes n ON n.id = notes_fts.rowid
            WHERE notes_fts MATCH @query
            ORDER BY rank
            LIMIT 50
            """;
        cmd.Parameters.AddWithValue("@query", query);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SearchResult(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }
        return results;
    }

    public List<string> GetNotesByTag(string tagName)
    {
        var results = new List<string>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.filepath
            FROM notes n
            JOIN note_tags nt ON nt.note_id = n.id
            JOIN tags t ON t.id = nt.tag_id
            WHERE t.name = @tagName
            ORDER BY n.filepath
            """;
        cmd.Parameters.AddWithValue("@tagName", tagName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }

    public List<TagCount> GetAllTagsWithCounts()
    {
        var results = new List<TagCount>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.name, COUNT(nt.note_id) as cnt
            FROM tags t
            JOIN note_tags nt ON nt.tag_id = t.id
            GROUP BY t.id
            ORDER BY cnt DESC, t.name
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new TagCount(reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    public Stats GetStats()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM notes),
                (SELECT COUNT(*) FROM tags),
                (SELECT COUNT(*) FROM note_tags)
            """;
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return new Stats(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    public void Dispose()
    {
        _conn.Dispose();
    }
}

public record SearchResult(string Filepath, string Filename, string Snippet);
public record TagCount(string Name, int Count);
public record Stats(int NoteCount, int TagCount, int LinkCount);
