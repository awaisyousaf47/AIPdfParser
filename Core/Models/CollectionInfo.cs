namespace Core.Models;

public class CollectionInfo
{
    public string Name { get; set; } = string.Empty;
    public int PointsCount { get; set; }
    public int VectorsCount { get; set; }
    public int IndexedVectorsCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public long SegmentsCount { get; set; }
    public Dictionary<string, object>? Config { get; set; }
}