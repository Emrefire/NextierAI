namespace NexTierAI.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TechnologyType { get; set; } = string.Empty;
    public int Level { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
}