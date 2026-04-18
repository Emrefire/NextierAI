using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NexTierAI.Domain.Interfaces;

public interface ILlmService
{
    Task<string> GenerateResponseAsync(string userQuery, string contextChunks);
    Task<float[]> GenerateEmbeddingAsync(string text);

    // YENİ: Streaming metodunun imzası
    IAsyncEnumerable<string> GenerateResponseStreamAsync(string userQuery, string contextChunks, CancellationToken cancellationToken = default);
}