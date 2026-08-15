using Seido.Utilities.SeedGenerator;
using Models.Interfaces;

namespace Models;

public class Album : IAlbum, ISeed<Album>
{
    public virtual Guid AlbumId { get; set; }

    public virtual string Name { get; set; }

    //Model relationships
    public virtual IMusicGroup MusicGroup { get; set; } = null;

    #region Constructors
    public Album() { }
    public Album(Album org)
    {
        this.Seeded = org.Seeded;

        this.AlbumId = org.AlbumId;
        this.Name = org.Name;
    }
    #endregion

    #region randomly seed this instance
    public virtual bool Seeded { get; set; } = false;
    public virtual Album Seed(SeedGenerator seedGenerator)
    {
        Seeded = true;
        AlbumId = Guid.NewGuid();

        Name = seedGenerator.MusicAlbumName;
        return this;
    }
    #endregion
}


