using System;

namespace Configuration;

public interface IMusicGenreService
{
    public string[] ReadMusicGenres();
}

public class Classics : IMusicGenreService
{

    public string[] ReadMusicGenres() => new string[] { "Classical", "Jazz", "Rock" };
}

public class Modern : IMusicGenreService
{

    public string[] ReadMusicGenres() => new string[] { "Blues", "Pop", "Electronic" };
}
