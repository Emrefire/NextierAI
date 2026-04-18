using System.Text;
using System.Text.Json;
using NexTierAI.Domain.Interfaces;

namespace NexTierAI.Infrastructure.Services;

public class OllamaService : ILlmService
{
    private readonly HttpClient _httpClient;
    private const string OllamaUrl = "http://localhost:11434";

    public OllamaService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(OllamaUrl),
            Timeout = TimeSpan.FromMinutes(2) // Llama ilk açılışta düşünürken süre yetmeyebilir, 2 dakika iyidir
        };
    }
    // YENİ: Canlı Akış (Streaming) Metodu
    public async IAsyncEnumerable<string> GenerateResponseStreamAsync(string userQuery, string contextChunks, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Eskisini silip bu iki satırı yapıştır (Hem Stream hem normal metot için)
        // 1. SİSTEM KURALLARI (İşten kaytarmasını kesin olarak yasaklıyoruz)
        var systemPrompt = @"Senin adın NexTier AI. Sen Türkçe konuşan, Senior (Kıdemli) Full-Stack Yazılım Mimarısın. Kullanıcıya her zaman samimi bir şekilde 'knk' diye hitap et. 
KESİN KURALLAR: 
1. Asla 'araştırmanızı öneririm', 'detaylara girmeyeyim', 'kafanız karışmasın' gibi amatör ifadeler kullanma! 
2. Sorulan konuyu (örneğin Clean Architecture) en ince detayına kadar, katman katman mimariyi anlatarak cevapla. 
3. 'Bana verilen bilgiye göre' deme, sanki kendi tecrübenmiş gibi aktar.";

        // 2. BAĞLAM (RAG) ENJEKSİYONU (Kafasının karışmasını engelliyoruz)
        string finalUserMessage = string.IsNullOrWhiteSpace(contextChunks) || contextChunks.Length < 5
            ? userQuery
            : $"AŞAĞIDAKİ BİLGİLERİ OKU, SADECE SORUYLA ALAKALI OLANLARI KULLAN (Alakasız bilgileri tamamen görmezden gel):\n\nBİLGİLER:\n{contextChunks}\n\nKULLANICI SORUSU: {userQuery}\n\nLütfen bir Senior Mimar gibi detaylı, uzun ve açıklayıcı bir cevap yaz.";

        var requestBody = new
        {
            model = "qwen2.5:3b",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = finalUserMessage }
            },
            stream = true, // SİHİR BURADA: Tüm cevabı beklemek yok!
            options = new { temperature = 0.3, top_p = 0.9, repeat_penalty = 1.1, num_thread = 8 }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        // HttpCompletionOption.ResponseHeadersRead: Karşı taraf yazmaya başladığı an bağlantıyı açar, cevabın bitmesini beklemez!
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
            {
                using var jsonDocument = JsonDocument.Parse(line);
                var root = jsonDocument.RootElement;
                if (root.TryGetProperty("message", out var messageProp) && messageProp.TryGetProperty("content", out var contentProp))
                {
                    var chunk = contentProp.GetString();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        // Kelimeyi yakalar yakalamaz ekrana fırlat
                        yield return chunk.Replace("Sistem Notu", "");
                    }
                }
            }
        }
    }
    public async Task<string> GenerateResponseAsync(string userQuery, string contextChunks)
    {
        // Qwen 2.5 çok zekidir, uzun uzun yasaklar koymaya gerek yok. Ne istediğimizi net söylememiz yeterli.
        // Eskisini silip bu iki satırı yapıştır (Hem Stream hem normal metot için)
        // 1. SİSTEM KURALLARI (İşten kaytarmasını kesin olarak yasaklıyoruz)
        var systemPrompt = @"Senin adın NexTier AI. Sen Türkçe konuşan, Senior (Kıdemli) Full-Stack Yazılım Mimarısın. Kullanıcıya her zaman samimi bir şekilde 'knk' diye hitap et. 
KESİN KURALLAR: 
1. Asla 'araştırmanızı öneririm', 'detaylara girmeyeyim', 'kafanız karışmasın' gibi amatör ifadeler kullanma! 
2. Sorulan konuyu (örneğin Clean Architecture) en ince detayına kadar, katman katman mimariyi anlatarak cevapla. 
3. 'Bana verilen bilgiye göre' deme, sanki kendi tecrübenmiş gibi aktar.";

        // 2. BAĞLAM (RAG) ENJEKSİYONU (Kafasının karışmasını engelliyoruz)
        string finalUserMessage = string.IsNullOrWhiteSpace(contextChunks) || contextChunks.Length < 5
            ? userQuery
            : $"AŞAĞIDAKİ BİLGİLERİ OKU, SADECE SORUYLA ALAKALI OLANLARI KULLAN (Alakasız bilgileri tamamen görmezden gel):\n\nBİLGİLER:\n{contextChunks}\n\nKULLANICI SORUSU: {userQuery}\n\nLütfen bir Senior Mimar gibi detaylı, uzun ve açıklayıcı bir cevap yaz.";
        var requestBody = new
        {
            model = "qwen2.5:3b", // <-- YENİ NESİL TÜRKÇE CANAVARI
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = finalUserMessage }
            },
            stream = false,
            options = new
            {
                temperature = 0.3,   // Teknik doğruluk için yaratıcılığı düşük tutuyoruz
                top_p = 0.9,
                repeat_penalty = 1.1,
                num_thread = 8
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/api/chat", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(responseString);

            var rawResponse = jsonDocument.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";

            // Qwen notları basmaz ama her ihtimale karşı temizleyelim
            rawResponse = rawResponse.Replace("Sistem Notu", "").Trim();

            return rawResponse;
        }
        catch (Exception ex)
        {
            return $"Sistemsel bir hata oluştu knk: {ex.Message}";
        }
    }
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var requestBody = new { model = "nomic-embed-text", prompt = text };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/embeddings", content);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(responseString);
        return jsonDoc.RootElement.GetProperty("embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray();
    }
}