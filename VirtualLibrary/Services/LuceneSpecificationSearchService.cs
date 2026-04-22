using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Services
{
    public sealed class LuceneSpecificationSearchService
    {
        private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
        private readonly AppDbContext _context;
        private readonly PdfService _pdfService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LuceneSpecificationSearchService> _logger;

        public LuceneSpecificationSearchService(
            AppDbContext context,
            PdfService pdfService,
            IWebHostEnvironment env,
            ILogger<LuceneSpecificationSearchService> logger)
        {
            _context = context;
            _pdfService = pdfService;
            _env = env;
            _logger = logger;
        }

        public sealed class SpecificationSearchResult
        {
            public int ProductId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Author { get; set; }
            public string? PdfPath { get; set; }
            public float Score { get; set; }
        }

        private string IndexDirectoryPath =>
            Path.Combine(_env.ContentRootPath, "LuceneIndex", "Specifications");

        private FSDirectory OpenIndexDirectory()
        {
            System.IO.Directory.CreateDirectory(IndexDirectoryPath);
            return FSDirectory.Open(IndexDirectoryPath);
        }

        private IndexWriter CreateWriter(FSDirectory directory, OpenMode mode = OpenMode.CREATE_OR_APPEND)
        {
            var analyzer = new StandardAnalyzer(AppLuceneVersion);
            var config = new IndexWriterConfig(AppLuceneVersion, analyzer)
            {
                OpenMode = mode,
                Similarity = new BM25Similarity()
            };
            return new IndexWriter(directory, config);
        }

        private IndexSearcher CreateSearcher(DirectoryReader reader)
        {
            return new IndexSearcher(reader) { Similarity = new BM25Similarity() };
        }

        public async Task RebuildIndexAsync()
        {
            using var directory = OpenIndexDirectory();
            using var writer = CreateWriter(directory, OpenMode.CREATE);

            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .ToListAsync();

            foreach (var product in products)
            {
                var doc = await BuildDocumentAsync(product);
                if (doc != null)
                    writer.AddDocument(doc);
            }

            writer.Flush(triggerMerge: false, applyAllDeletes: true);
            writer.Commit();
        }

        public async Task IndexProductAsync(int productId)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == productId);

            using var directory = OpenIndexDirectory();
            using var writer = CreateWriter(directory);

            writer.DeleteDocuments(new Term("ProductId", productId.ToString()));

            if (product != null)
            {
                var doc = await BuildDocumentAsync(product);
                if (doc != null)
                    writer.AddDocument(doc);
            }

            writer.Flush(triggerMerge: false, applyAllDeletes: true);
            writer.Commit();
        }

        public Task RemoveProductAsync(int productId)
        {
            using var directory = OpenIndexDirectory();
            using var writer = CreateWriter(directory);
            writer.DeleteDocuments(new Term("ProductId", productId.ToString()));
            writer.Flush(triggerMerge: false, applyAllDeletes: true);
            writer.Commit();
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<SpecificationSearchResult>> SearchAsync(
            string? queryText,
            string sort = "score_desc",
            int take = 50)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return Array.Empty<SpecificationSearchResult>();

            using var directory = OpenIndexDirectory();

            if (!DirectoryReader.IndexExists(directory))
                return Array.Empty<SpecificationSearchResult>();

            using var reader = DirectoryReader.Open(directory);

            if (reader.NumDocs == 0)
                return Array.Empty<SpecificationSearchResult>();

            var searcher = CreateSearcher(reader);
            var analyzer = new StandardAnalyzer(AppLuceneVersion);
            var parser = new QueryParser(AppLuceneVersion, "SearchText", analyzer)
            {
                DefaultOperator = Operator.AND
            };

            Query query;
            try
            {
                query = parser.Parse(QueryParserBase.Escape(queryText.Trim()));
            }
            catch (ParseException ex)
            {
                _logger.LogWarning(ex, "Invalid query: {QueryText}", queryText);
                return Array.Empty<SpecificationSearchResult>();
            }

            var topDocs = searcher.Search(query, Math.Max(take * 5, 100));
            var results = topDocs.ScoreDocs
                .Select(sd => new SpecificationSearchResult
                {
                    ProductId = int.TryParse(searcher.Doc(sd.Doc).Get("ProductId"), out var id) ? id : 0,
                    Title = searcher.Doc(sd.Doc).Get("Title") ?? "Untitled",
                    Author = searcher.Doc(sd.Doc).Get("Author"),
                    PdfPath = searcher.Doc(sd.Doc).Get("PdfPath"),
                    Score = sd.Score
                })
                .GroupBy(x => $"{x.Title}|||{x.Author}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.Score).First());

            var ordered = sort?.ToLowerInvariant() == "score_asc"
                ? results.OrderBy(x => x.Score)
                : results.OrderByDescending(x => x.Score);

            return ordered.Take(take).ToList();
        }

        private async Task<Document?> BuildDocumentAsync(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.DescriptionPdfPath))
                return null;

            var cleanPath = product.DescriptionPdfPath.TrimStart('/', '\\');
            var fullPdfPath = Path.Combine(
                _env.WebRootPath,
                cleanPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPdfPath))
                return null;

            string extractedText;
            try
            {
                extractedText = await _pdfService.ExtractTextAsync(fullPdfPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PDF extraction failed for product {ProductId}", product.Id);
                return null;
            }

            if (string.IsNullOrWhiteSpace(extractedText))
                return null;

            return new Document
            {
                new StringField("ProductId", product.Id.ToString(), Field.Store.YES),
                new TextField("Title", product.Title ?? string.Empty, Field.Store.YES),
                new TextField("Author", product.Author ?? string.Empty, Field.Store.YES),
                new StoredField("PdfPath", cleanPath.Replace('\\', '/')),
                new TextField("SearchText",
                    string.Join(" ", extractedText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
                    Field.Store.NO)
            };
        }
    }
}