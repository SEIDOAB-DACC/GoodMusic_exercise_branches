namespace Models.DTO;


public class GstUsrInfoDbDto
{
    public int NrSeededMusicGroups { get; set; } = 0;
    public int NrUnseededMusicGroups { get; set; } = 0;

    public int NrSeededAlbums { get; set; } = 0;
    public int NrUnseededAlbums { get; set; } = 0;
}
public class GstUsrInfoAllDto
{
    public GstUsrInfoDbDto Db { get; set; } = null;
}


