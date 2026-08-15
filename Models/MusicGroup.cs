using Seido.Utilities.SeedGenerator;
using Models.Interfaces;

namespace Models;

public class MusicGroup : IMusicGroup, ISeed<MusicGroup>
{
    public virtual Guid MusicGroupId { get; set; }
    public virtual string Name { get; set; }
    public virtual MusicGenre Genre { get; set; }

    //Model relationships
    public virtual List<IAlbum> Albums { get; set; } = new List<IAlbum>();

 
    #region Constructors
    public MusicGroup(){}
    public MusicGroup(MusicGroup org)
    {
        Seeded = org.Seeded;

        MusicGroupId = org.MusicGroupId;
        Name = org.Name;
        Genre = org.Genre;
    }
    #endregion

    #region randomly seed this instance
    public virtual bool Seeded { get; set; } = false;
    public virtual MusicGroup Seed(SeedGenerator seedGenerator)
    {
        Seeded = true;
        MusicGroupId = Guid.NewGuid();

        Name = seedGenerator.MusicGroupName;
        Genre = seedGenerator.FromEnum<MusicGenre>();
        return this;
    }
    #endregion
}


