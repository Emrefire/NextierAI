using NexTierAI.Domain.Entities;
using NexTierAI.Domain.Interfaces;

namespace NexTierAI.Application.Services;

public class MentorOrchestrator
{
    private readonly ILlmService _llmService;
    private readonly IVectorDbService _vectorDb;

    // Dependency Injection (Bağımlılık Enjeksiyonu) ile servisleri alıyoruz
    public MentorOrchestrator(ILlmService llmService, IVectorDbService vectorDb)
    {
        _llmService = llmService;
        _vectorDb = vectorDb;
    }

    // 1. ANA AKIŞ: Kullanıcı soru sorar, bot cevaplar (RAG)
    public async Task<string> AskQuestionAsync(string question)
    {
        // Adım A: Veritabanından soruya en çok benzeyen 3 metin parçasını bul
        var similarChunks = await _vectorDb.SearchSimilarAsync(question, topK: 3);

        // Adım B: Gelen parçaları alt alta ekleyerek yapay zekaya vereceğimiz 'Bağlam'ı (Context) oluştur
        var contextText = string.Join("\n\n---\n\n", similarChunks.Select(c => c.Content));

        // Adım C: Soru ve bağlamı phi3 modeline gönderip cevabı al
        var answer = await _llmService.GenerateResponseAsync(question, contextText);

        return answer;
    }
    // YENİ: Arayüz ile LLM arasındaki köprü (Streaming Versiyonu)
    // GÜNCELLENDİ: Hafıza (RAG) artık Canlı Akışa Bağlı!
    public async IAsyncEnumerable<string> AskQuestionStreamAsync(string text, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. Veritabanından soruya en çok benzeyen 3 metin parçasını bul (HAFIZA)
        var similarChunks = await _vectorDb.SearchSimilarAsync(text, topK: 3);

        // 2. Parçaları birleştirip bağlam oluştur (Hiçbir şey bulamazsa boş kalır)
        string contextChunks = similarChunks.Any()
            ? string.Join("\n\n---\n\n", similarChunks.Select(c => c.Content))
            : "";

        // 3. Gelen kelimeleri hem bağlamla hem soruyla beraber arayüze akıt
        await foreach (var chunk in _llmService.GenerateResponseStreamAsync(text, contextChunks, cancellationToken))
        {
            yield return chunk;
        }
    }
    // 2. ÖĞRETME AKIŞI: Sisteme yeni bir metin/döküman kaydetme
    public async Task IngestKnowledgeAsync(string rawText, string category, string techType, int level, string sourceName)
    {
        // Metni yapay zekanın sindirebileceği 500 karakterlik parçalara böl (Chunking)
        var chunks = ChunkText(rawText, 500);
        var documentChunks = new List<DocumentChunk>();

        foreach (var chunkText in chunks)
        {
            // Her parça için matematiksel vektör (Embedding) oluştur
            var embedding = await _llmService.GenerateEmbeddingAsync(chunkText);

            documentChunks.Add(new DocumentChunk
            {
                Content = chunkText,
                Category = category,
                TechnologyType = techType,
                Level = level,
                SourceFileName = sourceName,
                Embedding = embedding
            });
        }

        // Oluşan tüm vektörleri yerel JSON veritabanımıza kaydet
        await _vectorDb.AddDocumentChunksAsync(documentChunks);
    }

    // Metni mantıklı parçalara bölen yardımcı metod
    private IEnumerable<string> ChunkText(string text, int chunkSize)
    {
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
        }
    }
}