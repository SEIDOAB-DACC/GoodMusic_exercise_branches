using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

using Seido.Utilities.SeedGenerator;
using Models;
using Models.Interfaces;
using Models.DTO;

namespace DbModels;

[Table("Albums", Schema = "supusr")]
public class AlbumDbM : Album, ISeed<AlbumDbM>
{
    [Key]
    public override Guid AlbumId { get; set; }
    
    [Required]
    public override string Name { get; set; }
    
    #region implementing entity Navigation properties when model is using interfaces in the relationships between models
    [NotMapped]
    public override IMusicGroup MusicGroup { get => MusicGroupDbM; set => new NotImplementedException(); }
    [JsonIgnore] 
    [Required]
    public virtual MusicGroupDbM MusicGroupDbM { get; set; } = null;
    #endregion

    #region Constructors
    public AlbumDbM()
    {
        AlbumId = Guid.NewGuid();
    }
    public AlbumDbM(AlbumCUdto dto):this()
    {
        UpdateFromDTO(dto);
    }
    #endregion

    #region Update from DTO
    public AlbumDbM UpdateFromDTO(AlbumCUdto dto)
    {
        Name = dto.Name;
        return this;
    }
    #endregion

    #region randomly seed this instance
    public override AlbumDbM Seed(SeedGenerator sgen)
    {
        base.Seed(sgen);
        return this;
    }
    #endregion
}


