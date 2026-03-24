using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using InvoiceWizard.Data.Entities;

namespace InvoiceWizard.Services;

public sealed class DatanormCatalogService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Encoding _encoding;
    private List<DatanormArticleEntity>? _cache;
    private DatanormCatalogMetadata _metadata = new();

    public DatanormCatalogService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _encoding = Encoding.GetEncoding(850);
    }

    private string StorageDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvoiceWizard");

    private string StoragePath => Path.Combine(StorageDirectory, "datanorm-catalog.json.gz");

    public async Task<DatanormCatalogState> GetStateAsync()
    {
        await EnsureLoadedAsync();
        return new DatanormCatalogState
        {
            ArticleCount = _cache?.Count ?? 0,
            SourceFileName = _metadata.SourceFileName,
            ImportedAt = _metadata.ImportedAt
        };
    }

    public async Task<DatanormCatalogImportResult> ImportAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("Die DATANORM-Datei wurde nicht gefunden.", filePath);
        }

        var bundle = ResolveBundle(filePath);
        var articles = await ParseBundleAsync(bundle);
        if (articles.Count == 0)
        {
            throw new InvalidOperationException("Aus dem DATANORM-Bundle konnten keine verwertbaren Artikel gelesen werden.");
        }

        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(StorageDirectory);
            _cache = articles
                .GroupBy(x => BuildKey(x))
                .Select(g => g.First())
                .OrderBy(x => x.ArticleNumber)
                .ThenBy(x => x.Description)
                .ToList();
            _metadata = new DatanormCatalogMetadata
            {
                SourceFileName = Path.GetFileName(bundle.ArticleFilePath),
                ImportedAt = DateTime.Now
            };

            var payload = new DatanormCatalogStorage
            {
                SourceFileName = _metadata.SourceFileName,
                ImportedAt = _metadata.ImportedAt,
                Articles = _cache
            };
            await using var fileStream = File.Create(StoragePath);
            await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
            await JsonSerializer.SerializeAsync(gzipStream, payload, _jsonOptions);
        }
        finally
        {
            _lock.Release();
        }

        return new DatanormCatalogImportResult
        {
            ImportedCount = _cache.Count,
            SourceFileName = _metadata.SourceFileName,
            ImportedAt = _metadata.ImportedAt
        };
    }

    public async Task<IReadOnlyList<DatanormArticleEntity>> SearchAsync(string query, int maxResults = 200)
    {
        await EnsureLoadedAsync();
        var articles = _cache ?? [];
        if (string.IsNullOrWhiteSpace(query))
        {
            return articles.Take(maxResults).ToList();
        }

        var tokens = query
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToList();

        return articles
            .Select(article => new
            {
                Article = article,
                Score = ScoreArticle(article, tokens)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Article.ArticleNumber)
            .Select(x => x.Article)
            .Take(maxResults)
            .ToList();
    }

    public async Task<bool> HasCatalogAsync()
    {
        var state = await GetStateAsync();
        return state.ArticleCount > 0;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_cache is not null)
        {
            return;
        }

        await _lock.WaitAsync();
        try
        {
            if (_cache is not null)
            {
                return;
            }

            if (!File.Exists(StoragePath))
            {
                _cache = [];
                _metadata = new DatanormCatalogMetadata();
                return;
            }

            await using var fileStream = File.OpenRead(StoragePath);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            var payload = await JsonSerializer.DeserializeAsync<DatanormCatalogStorage>(gzipStream, _jsonOptions) ?? new DatanormCatalogStorage();
            _cache = payload.Articles ?? [];
            _metadata = new DatanormCatalogMetadata
            {
                SourceFileName = payload.SourceFileName,
                ImportedAt = payload.ImportedAt
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<DatanormArticleEntity>> ParseBundleAsync(DatanormBundle bundle)
    {
        var categories = await ParseCategoriesAsync(bundle.GroupFilePath);
        var rawArticles = await ParseArticleLinesAsync(bundle.ArticleFilePath);
        var prices = await ParsePricesAsync(bundle.PriceFilePath);

        foreach (var article in rawArticles.Values)
        {
            if (prices.TryGetValue(article.ArticleNumber, out var netPrice))
            {
                article.NetPrice = netPrice;
            }

            article.Description = BuildDescription(article.Description, article.Description2, article.CategoryCode, categories);
            article.Description2 = string.Empty;
            article.SourceFileName = Path.GetFileName(bundle.ArticleFilePath);
        }

        return rawArticles.Values
            .Where(x => !string.IsNullOrWhiteSpace(x.ArticleNumber) && !string.IsNullOrWhiteSpace(x.Description))
            .Select(x => new DatanormArticleEntity
            {
                ArticleNumber = x.ArticleNumber,
                Ean = x.Ean,
                Description = x.Description,
                Unit = NormalizeUnit(x.Unit),
                NetPrice = PricingHelper.RoundCurrency(x.NetPrice),
                GrossListPrice = PricingHelper.RoundCurrency(x.GrossListPrice),
                MetalSurcharge = 0m,
                PriceBasisQuantity = x.PriceBasisQuantity <= 0m ? 1m : x.PriceBasisQuantity,
                SourceFileName = x.SourceFileName
            })
            .ToList();
    }

    private async Task<Dictionary<string, string>> ParseCategoriesAsync(string filePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath))
        {
            return result;
        }

        foreach (var line in ReadLines(filePath))
        {
            if (!line.StartsWith("S;", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(';');
            if (parts.Length < 4)
            {
                continue;
            }

            var code = parts[2].Trim();
            var name = parts[3].Trim();
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name))
            {
                result[code] = name;
            }
        }

        return await Task.FromResult(result);
    }

    private async Task<Dictionary<string, DatanormRawArticle>> ParseArticleLinesAsync(string filePath)
    {
        var result = new Dictionary<string, DatanormRawArticle>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in ReadLines(filePath))
        {
            if (line.StartsWith("A;", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(';');
                if (parts.Length < 12)
                {
                    continue;
                }

                var articleNumber = parts[2].Trim();
                if (string.IsNullOrWhiteSpace(articleNumber))
                {
                    continue;
                }

                result[articleNumber] = new DatanormRawArticle
                {
                    ArticleNumber = articleNumber,
                    Description = parts[4].Trim(),
                    Description2 = parts[5].Trim(),
                    Unit = parts[8].Trim(),
                    GrossListPrice = ParseScaledDecimal(parts[9], 2),
                    Ean = parts.Length > 10 ? parts[10].Trim() : string.Empty,
                    CategoryCode = parts.Length > 11 ? parts[11].Trim() : string.Empty,
                    PriceBasisQuantity = parts.Length > 6 ? ParseDecimalOrDefault(parts[6], 1m) : 1m
                };
            }
            else if (line.StartsWith("B;", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(';');
                if (parts.Length < 13)
                {
                    continue;
                }

                var articleNumber = parts[2].Trim();
                if (string.IsNullOrWhiteSpace(articleNumber))
                {
                    continue;
                }

                if (!result.TryGetValue(articleNumber, out var article))
                {
                    article = new DatanormRawArticle { ArticleNumber = articleNumber };
                    result[articleNumber] = article;
                }

                if (parts.Length > 3 && string.IsNullOrWhiteSpace(article.Description2))
                {
                    article.Description2 = parts[3].Trim();
                }

                var basisQuantity = parts.Length > 13 ? ParseDecimalOrDefault(parts[13], article.PriceBasisQuantity <= 0m ? 1m : article.PriceBasisQuantity) : article.PriceBasisQuantity;
                article.PriceBasisQuantity = basisQuantity <= 0m ? 1m : basisQuantity;
            }
        }

        return await Task.FromResult(result);
    }

    private async Task<Dictionary<string, decimal>> ParsePricesAsync(string filePath)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath))
        {
            return result;
        }

        foreach (var line in ReadLines(filePath))
        {
            if (!line.StartsWith("P;", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(';');
            if (parts.Length < 5)
            {
                continue;
            }

            for (var index = 2; index + 2 < parts.Length; index += 9)
            {
                var articleNumber = parts[index].Trim();
                if (string.IsNullOrWhiteSpace(articleNumber))
                {
                    continue;
                }

                var decimals = (int)ParseDecimal(parts[index + 1]);
                var rawPrice = ParseDecimal(parts[index + 2]);
                var divisor = 1m;
                for (var i = 0; i < Math.Max(0, decimals); i++)
                {
                    divisor *= 10m;
                }
                result[articleNumber] = divisor <= 0m ? rawPrice : rawPrice / divisor;
            }
        }

        return await Task.FromResult(result);
    }

    private IEnumerable<string> ReadLines(string filePath)
    {
        foreach (var line in File.ReadLines(filePath, _encoding))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    private static DatanormBundle ResolveBundle(string selectedFilePath)
    {
        var directory = Path.GetDirectoryName(selectedFilePath) ?? string.Empty;

        var articleFilePath = FindSibling(directory, "DATANORM.001", selectedFilePath);
        var priceFilePath = FindSibling(directory, "DATPREIS.001", selectedFilePath);
        var groupFilePath = FindSibling(directory, "DATANORM.WRG", selectedFilePath);

        if (string.IsNullOrWhiteSpace(articleFilePath) || !File.Exists(articleFilePath))
        {
            throw new FileNotFoundException("Im ausgewählten DATANORM-Bundle wurde keine DATANORM.001 gefunden.", articleFilePath);
        }

        return new DatanormBundle
        {
            ArticleFilePath = articleFilePath,
            PriceFilePath = priceFilePath,
            GroupFilePath = groupFilePath
        };
    }

    private static string FindSibling(string directory, string fileName, string selectedFilePath)
    {
        var siblingPath = Path.Combine(directory, fileName);
        if (File.Exists(siblingPath))
        {
            return siblingPath;
        }

        return Path.GetFileName(selectedFilePath).Equals(fileName, StringComparison.OrdinalIgnoreCase)
            ? selectedFilePath
            : string.Empty;
    }

    private static string BuildDescription(string description1, string description2, string categoryCode, IReadOnlyDictionary<string, string> categories)
    {
        var text = string.Join(" ", new[] { description1, description2 }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (categories.TryGetValue(categoryCode, out var categoryName) && !string.IsNullOrWhiteSpace(categoryName))
        {
            return $"{text} [{categoryName}]".Trim();
        }

        return text;
    }

    private static int ScoreArticle(DatanormArticleEntity article, IReadOnlyList<string> tokens)
    {
        var score = 0;
        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (string.Equals(article.ArticleNumber, token, StringComparison.OrdinalIgnoreCase))
            {
                score += 120;
                continue;
            }

            if (string.Equals(article.Ean, token, StringComparison.OrdinalIgnoreCase))
            {
                score += 120;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(article.ArticleNumber) && article.ArticleNumber.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 70;
            }

            if (!string.IsNullOrWhiteSpace(article.Ean) && article.Ean.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 70;
            }

            if (!string.IsNullOrWhiteSpace(article.Description) && article.Description.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 45;
            }
        }

        return score;
    }

    private static string BuildKey(DatanormArticleEntity article)
    {
        return $"{article.ArticleNumber}|{article.Ean}|{article.Description}".ToLowerInvariant();
    }

    private static string NormalizeUnit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "ST";
        }

        var unit = value.Trim().ToUpperInvariant();
        return unit switch
        {
            "LFM" => "M",
            "MTR" => "M",
            _ => unit
        };
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var text = value.Trim();
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var parsed))
        {
            return parsed;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static decimal ParseDecimalOrDefault(string? value, decimal fallback)
    {
        var parsed = ParseDecimal(value);
        return parsed > 0m ? parsed : fallback;
    }

    private static decimal ParseScaledDecimal(string? value, int decimals)
    {
        var raw = ParseDecimal(value);
        var divisor = (decimal)Math.Pow(10, decimals);
        return divisor <= 0m ? raw : raw / divisor;
    }

    private sealed class DatanormBundle
    {
        public string ArticleFilePath { get; set; } = "";
        public string PriceFilePath { get; set; } = "";
        public string GroupFilePath { get; set; } = "";
    }

    private sealed class DatanormRawArticle
    {
        public string ArticleNumber { get; set; } = "";
        public string Ean { get; set; } = "";
        public string Description { get; set; } = "";
        public string Description2 { get; set; } = "";
        public string Unit { get; set; } = "ST";
        public decimal NetPrice { get; set; }
        public decimal GrossListPrice { get; set; }
        public decimal PriceBasisQuantity { get; set; } = 1m;
        public string CategoryCode { get; set; } = "";
        public string SourceFileName { get; set; } = "";
    }

    private sealed class DatanormCatalogStorage
    {
        public string SourceFileName { get; set; } = "";
        public DateTime? ImportedAt { get; set; }
        public List<DatanormArticleEntity> Articles { get; set; } = [];
    }

    private sealed class DatanormCatalogMetadata
    {
        public string SourceFileName { get; set; } = "";
        public DateTime? ImportedAt { get; set; }
    }
}

public sealed class DatanormCatalogState
{
    public int ArticleCount { get; set; }
    public string SourceFileName { get; set; } = "";
    public DateTime? ImportedAt { get; set; }
}

public sealed class DatanormCatalogImportResult
{
    public int ImportedCount { get; set; }
    public string SourceFileName { get; set; } = "";
    public DateTime? ImportedAt { get; set; }
}
