namespace Models.Interfaces;


public enum MusicGenre {Rock, Blues, Jazz, Metal}
public interface IMusicGroup
{
    public Guid MusicGroupId { get; set; }
    public string Name { get; set; }
    public List<IAlbum> Albums { get; set; }
}

public interface IAlbum
{
    public Guid AlbumId { get; set; }

    public string Name { get; set; }
    
    public IMusicGroup MusicGroup { get; set;} 
}