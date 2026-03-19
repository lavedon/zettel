using Spectre.Console;
using Zettel;

bool plain = false;
bool caseSensitive = false;
bool paged = false;
string? dbPath = null;
string? command = null;
string? commandArg = null;

// Parse arguments
for (int i = 0; i < args.Length; i++)
{
    var arg = args[i].TrimStart('-').ToLowerInvariant();

    if (arg is "help" or "h" or "?")
    {
        PrintUsage();
        return 0;
    }

    if (arg == "plain")
    {
        plain = true;
        continue;
    }

    if (arg is "case-sensitive" or "cs")
    {
        caseSensitive = true;
        continue;
    }

    if (arg is "paged" or "p")
    {
        paged = true;
        continue;
    }

    if (arg is "db")
    {
        if (i + 1 >= args.Length)
        {
            PrintError("--db requires a path. Example: --db vault.db");
            return 1;
        }
        dbPath = args[++i];
        continue;
    }

    if (!args[i].StartsWith('-'))
    {
        if (command is null)
        {
            command = arg;
            continue;
        }
        if (commandArg is null)
        {
            commandArg = args[i]; // preserve original case for paths/queries
            continue;
        }
    }

    PrintError($"Unknown argument: {args[i]}");
    PrintUsage();
    return 1;
}

dbPath ??= @"C:\tools\Data\zettel.db";
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

// No command → interactive REPL
if (command is null)
{
    if (Console.IsInputRedirected)
    {
        PrintError("Pipe a command, e.g.: echo query | zettel search");
        return 1;
    }
    return RunInteractive();
}

// Dispatch command
using var db = new Database(dbPath);

switch (command)
{
    case "index":
        return RunIndex(db, commandArg);

    case "search":
        var query = commandArg;
        if (query is null && Console.IsInputRedirected)
            query = Console.In.ReadToEnd().Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            PrintError("Usage: zettel search <query>");
            return 1;
        }
        RunSearch(db, query, paged);
        return 0;

    case "tag":
        if (string.IsNullOrWhiteSpace(commandArg))
        {
            PrintError("Usage: zettel tag <tag-name>");
            return 1;
        }
        RunTag(db, commandArg, caseSensitive);
        return 0;

    case "tags":
        RunTags(db);
        return 0;

    case "stats":
        RunStats(db);
        return 0;

    default:
        PrintError($"Unknown command: {command}");
        PrintUsage();
        return 1;
}

// ── Commands ────────────────────────────────────────────────────────

int RunIndex(Database db, string? vaultPath)
{
    if (string.IsNullOrWhiteSpace(vaultPath))
    {
        PrintError("Usage: zettel index <vault-path>");
        return 1;
    }

    if (!Directory.Exists(vaultPath))
    {
        PrintError($"Directory not found: {vaultPath}");
        return 1;
    }

    if (!plain)
        AnsiConsole.MarkupLine($"[dim]Indexing {Markup.Escape(Path.GetFullPath(vaultPath))}...[/]");

    var result = Indexer.Index(vaultPath, db);

    if (plain)
    {
        Console.WriteLine($"Added: {result.Added}, Updated: {result.Updated}, Removed: {result.Removed}, Unchanged: {result.Unchanged}");
    }
    else
    {
        AnsiConsole.MarkupLine($"[green]Added:[/] {result.Added}  [yellow]Updated:[/] {result.Updated}  [red]Removed:[/] {result.Removed}  [dim]Unchanged:[/] {result.Unchanged}");
    }
    return 0;
}

void RunSearch(Database db, string query, bool paged = false)
{
    var results = db.Search(query);

    if (results.Count == 0)
    {
        PrintWarning("No results found.");
        return;
    }

    if (plain)
    {
        foreach (var r in results)
        {
            Console.WriteLine(r.Filepath);
            Console.WriteLine($"  {r.Snippet}");
            Console.WriteLine("────────────────────────────────────────");
        }
    }
    else if (paged)
    {
        AnsiConsole.MarkupLine($"[bold blue]Search: {Markup.Escape(query)}[/]  [dim]({results.Count} results)[/]");
        AnsiConsole.WriteLine();

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var snippet = Markup.Escape(r.Snippet)
                .Replace(">>>", "[bold yellow]")
                .Replace("<<<", "[/]");

            AnsiConsole.MarkupLine($"[dim]({i + 1}/{results.Count})[/]  [green]{Markup.Escape(r.Filename)}[/]");
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(r.Filepath)}[/]");
            AnsiConsole.MarkupLine(snippet);
            AnsiConsole.Write(new Rule().RuleStyle("dim"));

            if (i < results.Count - 1)
            {
                AnsiConsole.MarkupLine("[dim]Enter = next, q = quit[/]");
                var key = Console.ReadKey(true);
                if (key.KeyChar is 'q' or 'Q')
                    break;
                AnsiConsole.WriteLine();
            }
        }
    }
    else
    {
        AnsiConsole.MarkupLine($"[bold blue]Search: {Markup.Escape(query)}[/]  [dim]({results.Count} results)[/]");
        AnsiConsole.WriteLine();

        foreach (var r in results)
        {
            var snippet = Markup.Escape(r.Snippet)
                .Replace(">>>", "[bold yellow]")
                .Replace("<<<", "[/]");

            AnsiConsole.MarkupLine($"[green]{Markup.Escape(r.Filename)}[/]");
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(r.Filepath)}[/]");
            AnsiConsole.MarkupLine(snippet);
            AnsiConsole.Write(new Rule().RuleStyle("dim"));
        }
    }
}

void RunTag(Database db, string tagName, bool caseSensitive = false)
{
    var files = db.GetNotesByTag(tagName, caseSensitive);

    if (files.Count == 0)
    {
        PrintWarning($"No notes found with tag: {tagName}");
        return;
    }

    if (plain)
    {
        Console.WriteLine($"Tag: {tagName} ({files.Count} notes)");
        foreach (var f in files)
            Console.WriteLine($"  {f}");
    }
    else
    {
        AnsiConsole.MarkupLine($"[bold blue]{Markup.Escape(tagName)}[/] [dim]({files.Count} notes)[/]");
        foreach (var f in files)
            AnsiConsole.MarkupLine($"  [green]{Markup.Escape(f)}[/]");
    }
}

void RunTags(Database db)
{
    var tags = db.GetAllTagsWithCounts();

    if (tags.Count == 0)
    {
        PrintWarning("No tags found. Run 'zettel index <path>' first.");
        return;
    }

    if (plain)
    {
        foreach (var t in tags)
            Console.WriteLine($"{t.Count}\t{t.Name}");
    }
    else
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold blue]Tags[/]  [dim]({tags.Count} total)[/]")
            .AddColumn(new TableColumn("[bold]Tag[/]"))
            .AddColumn(new TableColumn("[bold]Count[/]").RightAligned());

        foreach (var t in tags)
            table.AddRow($"[green]{Markup.Escape(t.Name)}[/]", t.Count.ToString());

        AnsiConsole.Write(table);
    }
}

void RunStats(Database db)
{
    var stats = db.GetStats();

    if (plain)
    {
        Console.WriteLine($"Notes: {stats.NoteCount}");
        Console.WriteLine($"Tags: {stats.TagCount}");
        Console.WriteLine($"Tag links: {stats.LinkCount}");
    }
    else
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold blue]Stats[/]")
            .AddColumn(new TableColumn("[bold]Metric[/]"))
            .AddColumn(new TableColumn("[bold]Value[/]").RightAligned());

        table.AddRow("Notes", stats.NoteCount.ToString());
        table.AddRow("Tags", stats.TagCount.ToString());
        table.AddRow("Tag links", stats.LinkCount.ToString());

        AnsiConsole.Write(table);
    }
}

// ── Interactive ─────────────────────────────────────────────────────

int RunInteractive()
{
    AnsiConsole.MarkupLine("[bold blue]zettel[/] — [dim]interactive mode[/]");
    AnsiConsole.WriteLine();

    using var db = new Database(dbPath!);

    while (true)
    {
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold blue]What would you like to do?[/]")
                .AddChoices("Search notes", "Search notes (paged)", "Browse tags", "Index vault", "Stats", "Exit"));

        if (action == "Exit")
            return 0;

        if (action is "Search notes" or "Search notes (paged)")
        {
            var query = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Search query:[/]")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(query))
            {
                AnsiConsole.WriteLine();
                continue;
            }

            AnsiConsole.WriteLine();
            RunSearch(db, query, paged: action.Contains("paged"));
            AnsiConsole.WriteLine();
            continue;
        }

        if (action == "Browse tags")
        {
            var tags = db.GetAllTagsWithCounts();
            if (tags.Count == 0)
            {
                PrintWarning("No tags found. Index a vault first.");
                AnsiConsole.WriteLine();
                continue;
            }

            var choices = tags.Select(t => $"{t.Name} ({t.Count})").ToList();
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Select a tag:[/]")
                    .PageSize(20)
                    .AddChoices(choices));

            // Extract tag name (everything before the last " (N)")
            var tagName = selected[..selected.LastIndexOf(" (")];
            AnsiConsole.WriteLine();
            RunTag(db, tagName);
            AnsiConsole.WriteLine();
            continue;
        }

        if (action == "Index vault")
        {
            var path = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Vault path:[/]"));

            if (!Directory.Exists(path))
            {
                PrintError($"Directory not found: {path}");
                AnsiConsole.WriteLine();
                continue;
            }

            AnsiConsole.WriteLine();
            RunIndex(db, path);
            AnsiConsole.WriteLine();
            continue;
        }

        if (action == "Stats")
        {
            AnsiConsole.WriteLine();
            RunStats(db);
            AnsiConsole.WriteLine();
        }
    }
}

// ── Helpers ─────────────────────────────────────────────────────────

void PrintUsage()
{
    if (plain)
    {
        Console.WriteLine("zettel - Index and search your zettelkasten vault");
        Console.WriteLine();
        Console.WriteLine("Usage: zettel [options] <command> [argument]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  index <vault-path>   Index/re-index a vault directory");
        Console.WriteLine("  search <query>       Full-text search notes");
        Console.WriteLine("  tag <tag-name>       Find notes with a specific tag");
        Console.WriteLine("  tags                 List all tags with counts");
        Console.WriteLine("  stats                Show database statistics");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --db <path>          SQLite database path (default: C:\\tools\\Data\\zettel.db)");
        Console.WriteLine("  --plain              Plain output (no colors)");
        Console.WriteLine("  --case-sensitive     Case-sensitive tag lookup (default: case-insensitive)");
        Console.WriteLine("  --paged, -p          Step through results one at a time");
        Console.WriteLine("  --help, -h           Show this help");
        Console.WriteLine();
        Console.WriteLine("No arguments launches interactive mode.");
    }
    else
    {
        var panel = new Panel(
            new Rows(
                new Markup("[bold]Commands:[/]"),
                new Markup("  [green]index <vault-path>[/]   Index/re-index a vault directory"),
                new Markup("  [green]search <query>[/]       Full-text search notes"),
                new Markup("  [green]tag <tag-name>[/]       Find notes with a specific tag"),
                new Markup("  [green]tags[/]                 List all tags with counts"),
                new Markup("  [green]stats[/]                Show database statistics"),
                new Markup(""),
                new Markup("[bold]Options:[/]"),
                new Markup("  [green]--db <path>[/]          SQLite database path (default: C:\\tools\\Data\\zettel.db)"),
                new Markup("  [green]--plain[/]              Plain output (no colors)"),
                new Markup("  [green]--case-sensitive[/]     Case-sensitive tag lookup"),
                new Markup("  [green]--paged[/], [green]-p[/]         Step through results one at a time"),
                new Markup("  [green]--help[/], [green]-h[/]           Show this help"),
                new Markup(""),
                new Markup("[dim]No arguments launches interactive mode.[/]")
            ))
            .Header("[bold blue]zettel[/] — Index and search your zettelkasten vault")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
    }
}

void PrintError(string message)
{
    if (plain)
        Console.Error.WriteLine($"ERROR: {message}");
    else
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
}

void PrintWarning(string message)
{
    if (plain)
        Console.Error.WriteLine($"WARNING: {message}");
    else
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
}
