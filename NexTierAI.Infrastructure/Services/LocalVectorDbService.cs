using System.Text.Json;
using NexTierAI.Domain.Entities;
using NexTierAI.Domain.Interfaces;

namespace NexTierAI.Infrastructure.Services;

public class LocalVectorDbService : IVectorDbService
{
    private readonly ILlmService _llmService;
    private readonly string _dbPath;
    private List<DocumentChunk> _database;

    public LocalVectorDbService(ILlmService llmService)
    {
        _llmService = llmService;

        // Vektörlerimizi bilgisayarın güvenli LocalAppData klasöründe bir JSON dosyasında saklayacağız
        _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexTierAI_Vectors.json");
        _database = new List<DocumentChunk>();

        LoadDatabase(); // Uygulama açılırken eski hafızayı yükle
    }

    private void LoadDatabase()
    {
        if (File.Exists(_dbPath))
        {
            var json = File.ReadAllText(_dbPath);
            _database = JsonSerializer.Deserialize<List<DocumentChunk>>(json) ?? new List<DocumentChunk>();
        }
    }

    // Sisteme yeni PDF/Döküman öğretildiğinde bunu JSON dosyasına yazar
    public async Task AddDocumentChunksAsync(IEnumerable<DocumentChunk> chunks)
    {
        _database.AddRange(chunks);
        var json = JsonSerializer.Serialize(_database);
        await File.WriteAllTextAsync(_dbPath, json);
    }

    // RAG'ın Kalbi: Sorulan soruya en yakın metinleri bulur
    public async Task<IEnumerable<DocumentChunk>> SearchSimilarAsync(string query, int topK = 3)
    {
        if (!_database.Any()) return new List<DocumentChunk>();

        // 1. Önce kullanıcının sorduğu soruyu vektöre (sayılara) çeviriyoruz
        var queryEmbedding = await _llmService.GenerateEmbeddingAsync(query);

        // 2. Kosinüs Benzerliği formülü ile veritabanındaki tüm metinleri puanlayıp en yüksek olanları getiriyoruz
        var results = _database
            .Select(chunk => new
            {
                Chunk = chunk,
                Similarity = CosineSimilarity(queryEmbedding, chunk.Embedding)
            })
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();

        return results;
    }

    // Saf C# ve Matematik ile Yazılmış Yapay Zeka Benzerlik Algoritması (Cosine Similarity)
    private float CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length) return 0;

        float dotProduct = 0, magnitude1 = 0, magnitude2 = 0;
        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        if (magnitude1 == 0 || magnitude2 == 0) return 0;
        return (float)(dotProduct / (Math.Sqrt(magnitude1) * Math.Sqrt(magnitude2)));
    }
}