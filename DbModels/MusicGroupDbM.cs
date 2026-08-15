using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

using Seido.Utilities.SeedGenerator;
using Models;
using Models.Interfaces;


namespace DbModels;

public class MusicGroupDbM : MusicGroup, ISeed<MusicGroupDbM>
{
    [Key]       
    public override Guid MusicGroupId { get; set; }

    
    #region Constructors
    public MusicGroupDbM() : base() 
    {
        MusicGroupId = Guid.NewGuid();
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


