using System.Text.RegularExpressions;
using Models.Interfaces;


namespace Models.DTO;

//DTO is a DataTransferObject, can be instanstiated by the controller logic
//and represents a, fully instantiable, subset of the Database models
//for a specific purpose.

//These DTO are simplistic and used to Update and Create objects
public class MusicGroupCUdto
{
    public Guid? MusicGroupId { get; set; }
    public bool Seeded { get; set; } = true;

    public string Name { get; set; }
    public int EstablishedYear { get; set; }

    public MusicGenre Genre { get; set; }

    public List<Guid> AlbumsId { get; set; } = new List<Guid>();

    public MusicGroupCUdto() { }
    public MusicGroupCUdto(IMusicGroup model)
    {
        this.MusicGroupId = model.MusicGroupId;

        this.Name = model.Name;

        this.AlbumsId = model.Albums.Select(a => a.AlbumId).ToList();
    }
    public void EnsureValidity()
    {
        // RegEx check to ensure filter only contains a-z, 0-9, spaces, -, ., and !
        if (!string.IsNullOrEmpty(Name) && !Regex.IsMatch(Name, @"^[a-zA-Z0-9\s\-\.!]*$"))
        {
            throw new ArgumentException("Name can only contain letters (a-z), numbers (0-9), spaces, -, ., and !.");
        }
        if (EstablishedYear <= 0) throw new ArgumentException("EstablishedYear has to be larger than zero");
        if (!Enum.IsDefined(typeof(MusicGenre), Genre)) throw new ArgumentException("Genre has to be set to a valid value");
    }
}

public class AlbumCUdto
{
    public Guid? AlbumId { get; set; }
    public bool Seeded { get; set; } = true;

    public string Name { get; set; }

    //Navigation properties that EFC will use to build relations
    public Guid MusicGroupId { get; set; }


    public AlbumCUdto() { }
    public AlbumCUdto(IAlbum model)
    {
        this.AlbumId = model.AlbumId;
        this.Name = model.Name;

        this.MusicGroupId = model.MusicGroup.MusicGroupId;
    }
    public void EnsureValidity()
    {
        // RegEx check to ensure filter only contains a-z, 0-9, spaces, -, ., and !
        if (!string.IsNullOrEmpty(Name) && !Regex.IsMatch(Name, @"^[a-zA-Z0-9\s\-\.!]*$"))
        {
            throw new ArgumentException("Name can only contain letters (a-z), numbers (0-9), spaces, -, ., and !.");
        }
    }
}
