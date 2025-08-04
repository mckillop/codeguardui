namespace Atturra.CodeGuard;

public class FolderDto : IComparable<FolderDto>
{
    public string id { get; set; }
    public string fullPath { get; set; }
    public FolderDto(string id, string fullPath)
    {
        this.id = id;
        this.fullPath = fullPath;
    }
    public int CompareTo(FolderDto? other)
    {
        if (other == null) return 1;
        return string.Compare(fullPath, other.fullPath, StringComparison.Ordinal);
    }    
}