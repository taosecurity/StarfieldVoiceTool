using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Serilog;
using StarfieldVT.Core.Filesystem;
using StarfieldVT.Core.Models;
using Path = System.IO.Path;

namespace StarfieldVT.Core;

public class LuceneManager : IDisposable
{
    const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
    private static readonly string BasePath = AppDataFolder.GetAppDataFolder();
    private static readonly string IndexPath = Path.Join(BasePath, "index");
    private static readonly Dictionary<string, Analyzer> PerFieldAnalyzers = new()
    {
        { "master", new KeywordAnalyzer() }
    };
    private readonly Analyzer _analyzer = new PerFieldAnalyzerWrapper(new StandardAnalyzer(AppLuceneVersion), PerFieldAnalyzers);
    private readonly IndexWriterConfig _indexConfig;
    private readonly FSDirectory _dir;
    private readonly IndexWriter _writer;
    private static readonly Lazy<LuceneManager> LazyInstance = new(() => new LuceneManager());

    public static LuceneManager Instance() => LazyInstance.Value;

    private LuceneManager()
    {
        try
        {
            _indexConfig = new IndexWriterConfig(AppLuceneVersion, _analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };
            _dir = FSDirectory.Open(IndexPath);
            _writer = new IndexWriter(_dir, _indexConfig);
            _writer.Commit();
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to create a Lucene Manager {0}", e);
            throw;
        }
    }

    public void DeleteIndex()
    {
        _writer.DeleteAll();
        _writer.Commit();
    }

    public void AddDocumentWithoutCommitting(VoiceLine voiceLine)
    {
        Log.Debug(
            "Adding document with voice type {0} dialogue {1} to lucene index at {2}",
            voiceLine.VoiceType,
            voiceLine.Dialogue,
            IndexPath);
        var doc = new Document
        {
            new StringField("master", voiceLine.ModName, Field.Store.YES),
            new StringField("wemPath", voiceLine.Filename, Field.Store.YES),
            new TextField("dialogue", voiceLine.Dialogue, Field.Store.YES),
            new StringField("voiceType", voiceLine.VoiceType, Field.Store.YES)
        };

        _writer.AddDocument(doc);
    }

    public void CommitWrites()
    {
        _writer.Flush(triggerMerge: false, applyAllDeletes: false);
        _writer.Commit();
    }

    /// <summary>
    /// Parse what the user typed into the filter box. Lucene syntax (phrases, wildcards, AND/OR) works
    /// when it is well formed; anything else, like a lone quote or bracket, is searched literally.
    /// </summary>
    public static Query ParseUserQuery(QueryParser parser, string searchText)
    {
        try
        {
            return parser.Parse(searchText);
        }
        catch (ParseException)
        {
            // lowercase too, so a bare AND/OR/NOT is a word rather than an operator; the analyzer lowercases anyway
            return parser.Parse(QueryParserBase.Escape(searchText.ToLowerInvariant()));
        }
    }

    /// <param name="master">Restrict to this plugin, or search every plugin when null.</param>
    /// <param name="voiceType">Restrict to this voice type, or search every voice type when null.</param>
    public IReadOnlyList<VoiceLine> FreeTextSearch(string searchText, string? master, string? voiceType)
    {
        var parser = new QueryParser(AppLuceneVersion, "dialogue", _analyzer);
        var boolQuery = new BooleanQuery
        {
            { ParseUserQuery(parser, searchText), Occur.MUST }
        };

        if (master != null)
        {
            boolQuery.Add(new TermQuery(new Term("master", master)), Occur.MUST);
        }

        if (voiceType != null)
        {
            boolQuery.Add(new TermQuery(new Term("voiceType", voiceType)), Occur.MUST);
        }

        Log.Debug("Searching with query: {Query}", boolQuery.ToString());

        using var reader = DirectoryReader.Open(_dir);
        var searcher = new IndexSearcher(reader);
        var docs = searcher.Search(boolQuery, Math.Max(1, reader.MaxDoc));

        // enumerate them now, otherwise the reader will have been disposed
        return docs.ScoreDocs.Select(doc =>
        {
            var fullDoc = searcher.Doc(doc.Doc);
            return new VoiceLine(
                fullDoc.Get("wemPath"),
                fullDoc.Get("dialogue"),
                fullDoc.Get("master"),
                fullDoc.Get("voiceType")
            );
        }).ToList();
    }

    public void Dispose()
    {
        _writer.Dispose();
        _dir.Dispose();
    }
}
