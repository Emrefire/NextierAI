using NexTierAI.Domain.Entities;

namespace NexTierAI.Domain.Interfaces;

public interface IVectorDbService
{
    // Öğretme aşamasında PDF'ten okunan parçaları veritabanına kaydeder
    Task AddDocumentChunksAsync(IEnumerable<DocumentChunk> chunks);

    // Kullanıcı soru sorduğunda soruya en yakın metinleri bulup getirir
    Task<IEnumerable<DocumentChunk>> SearchSimilarAsync(string query, int topK = 3);
}