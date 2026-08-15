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
    public List<Guid> ArtistsId { get; set; } = new List<Guid>();

    public MusicGroupCUdto() { }
    public MusicGroupCUdto(IMusicGroup model)
    {
        this.MusicGroupId = model.MusicGroupId;

        this.Name = model.Name;

        this.AlbumsId = model.Albums.Select(a => a.AlbumId).ToList();
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
}
