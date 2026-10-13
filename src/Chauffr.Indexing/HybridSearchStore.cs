using System.Collections.Concurrent;
using Chauffr.Core.Models;

namespace Chauffr.Indexing;

public record SearchResultItem(
    ContentChunk Chunk,
    double DenseScore,
    double Bm25Score,
    double RrfScore
);

public class HybridSearchStore
{
    private readonly ConcurrentDictionary<string, ContentChunk> _chunks = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, int>> _documentTermFrequencies = new();
    private readonly ConcurrentDictionary<string, int> _documentFrequencies = new();
    private double _averageDocumentLength = 0;

    public void UpsertChunks(IEnumerable<ContentChunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            _chunks[chunk.Id] = chunk;
            IndexBm25(chunk);
        }
        RecalculateBm25Averages();
    }

    /// <summary>
    /// Executes Hybrid Search combining Dense Vector Cosine Similarity and Sparse BM25 Lexical scoring via RRF.
    /// </summary>
    public List<SearchResultItem> SearchHybrid(
        string query,
        ReadOnlyMemory<float>? queryVector = null,
        int topK = 5,
        int rrfConstantK = 60)
    {
        var denseRankings = new List<(string ChunkId, double Score)>();
        var bm25Rankings = new List<(string ChunkId, double Score)>();

        // 1. Dense Vector Search (if queryVector is provided)
        if (queryVector.HasValue)
        {
            foreach (var kvp in _chunks)
            {
                if (kvp.Value.DenseVector.HasValue)
                {
                    double sim = CosineSimilarity(queryVector.Value.Span, kvp.Value.DenseVector.Value.Span);
                    denseRankings.Add((kvp.Key, sim));
                }
            }
            denseRankings = denseRankings.OrderByDescending(x => x.Score).ToList();
        }

        // 2. BM25 Lexical Search
        var queryTerms = Tokenize(query);
        foreach (var kvp in _chunks)
        {
            double bm25 = CalculateBm25Score(kvp.Key, queryTerms);
            if (bm25 > 0)
            {
                bm25Rankings.Add((kvp.Key, bm25));
            }
        }
        bm25Rankings = bm25Rankings.OrderByDescending(x => x.Score).ToList();

        // 3. Reciprocal Rank Fusion (RRF)
        var rrfScores = new Dictionary<string, (double DenseScore, double Bm25Score, double Rrf)>();

        for (int rank = 0; rank < denseRankings.Count; rank++)
        {
            var (chunkId, score) = denseRankings[rank];
            double rrf = 1.0 / (rrfConstantK + (rank + 1));
            rrfScores[chunkId] = (score, 0, rrf);
        }

        for (int rank = 0; rank < bm25Rankings.Count; rank++)
        {
            var (chunkId, score) = bm25Rankings[rank];
            double rrfContribution = 1.0 / (rrfConstantK + (rank + 1));

            if (rrfScores.TryGetValue(chunkId, out var existing))
            {
                rrfScores[chunkId] = (existing.DenseScore, score, existing.Rrf + rrfContribution);
            }
            else
            {
                rrfScores[chunkId] = (0, score, rrfContribution);
            }
        }

        return rrfScores
            .OrderByDescending(x => x.Value.Rrf)
            .Take(topK)
            .Select(x => new SearchResultItem(
                _chunks[x.Key],
                x.Value.DenseScore,
                x.Value.Bm25Score,
                x.Value.Rrf
            ))
            .ToList();
    }

    private void IndexBm25(ContentChunk chunk)
    {
        var terms = Tokenize($"{chunk.Heading} {chunk.Content}");
        var termFreqs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in terms)
        {
            termFreqs[term] = termFreqs.GetValueOrDefault(term) + 1;
        }

        _documentTermFrequencies[chunk.Id] = termFreqs;

        foreach (var term in termFreqs.Keys)
        {
            _documentFrequencies.AddOrUpdate(term, 1, (_, count) => count + 1);
        }
    }

    private void RecalculateBm25Averages()
    {
        if (_documentTermFrequencies.IsEmpty) return;
        _averageDocumentLength = _documentTermFrequencies.Values.Average(dict => dict.Values.Sum());
    }

    private double CalculateBm25Score(string chunkId, List<string> queryTerms, double k1 = 1.5, double b = 0.75)
    {
        if (!_documentTermFrequencies.TryGetValue(chunkId, out var docTerms)) return 0;

        int docLength = docTerms.Values.Sum();
        int totalDocs = _chunks.Count;
        double score = 0;

        foreach (var term in queryTerms)
        {
            if (!docTerms.TryGetValue(term, out int tf)) continue;

            int df = _documentFrequencies.GetValueOrDefault(term, 1);
            double idf = Math.Log((totalDocs - df + 0.5) / (df + 0.5) + 1.0);

            double numerator = tf * (k1 + 1);
            double denominator = tf + k1 * (1 - b + b * (docLength / (_averageDocumentLength + 0.0001)));

            score += idf * (numerator / denominator);
        }

        return score;
    }

    private static List<string> Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '-', '_', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1)
            .ToList();
    }

    private static double CosineSimilarity(ReadOnlySpan<float> vecA, ReadOnlySpan<float> vecB)
    {
        if (vecA.Length != vecB.Length) return 0;
        double dot = 0, magA = 0, magB = 0;

        for (int i = 0; i < vecA.Length; i++)
        {
            dot += vecA[i] * vecB[i];
            magA += vecA[i] * vecA[i];
            magB += vecB[i] * vecB[i];
        }

        double denom = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denom == 0 ? 0 : dot / denom;
    }
}
