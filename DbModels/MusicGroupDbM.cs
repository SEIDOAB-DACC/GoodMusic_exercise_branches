using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

using Seido.Utilities.SeedGenerator;
using Models;
using Models.Interfaces;
using Models.DTO;

namespace DbModels;

[Table("MusicGroups", Schema = "supusr")]
public class MusicGroupDbM : MusicGroup, ISeed<MusicGroupDbM>
{
    [Key]       
    public override Guid MusicGroupId { get; set; }

    #region implementing entity Navigation properties when model is using interfaces in the relationships between models
    [NotMapped]
    public override List<IAlbum> Albums { get => AlbumsDbM?.ToList<IAlbum>(); set => new NotImplementedException(); }
    [JsonIgnore]
    public virtual List<AlbumDbM> AlbumsDbM { get; set; } = null;
    #endregion

    #region Constructors
    public MusicGroupDbM()
    {
        MusicGroupId = Guid.NewGuid();
    }
    public MusicGroupDbM(MusicGroupCUdto dto):this()
    {
        UpdateFromDTO(dto);
    }
    #endregion

    #region Update from DTO
    public MusicGroupDbM UpdateFromDTO(MusicGroupCUdto dto)
    {
        Name = dto.Name;
        return this;
    }
    #endregion


    #region randomly seed this instance
    public override MusicGroupDbM Seed(SeedGenerator seedGenerator)
    {
        base.Seed(seedGenerator);
        return this;
    }
    #endregion
}


