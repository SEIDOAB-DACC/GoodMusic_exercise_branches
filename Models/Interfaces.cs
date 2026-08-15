namespace Models.Interfaces;


public enum MusicGenre {Rock, Blues, Jazz, Metal}
public interface IMusicGroup
{
    public Guid MusicGroupId { get; set; }
    public string Name { get; set; }
}
