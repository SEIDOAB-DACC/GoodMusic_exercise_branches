namespace Configuration.Options;

public class AesEncryptionOptions
{
    public const string Position = "AesEncryption";
    public string Key { get; set; }
    public string Iv { get; set; }
    public string Salt { get; set; }
    public int Iterations { get; set; }

    public byte[] KeyHash { get; private set; }
    public byte[] IvHash { get; private set; }

    public void HashKeyIv(Func<int,string, byte[]> hasher)
    {
        KeyHash = hasher.Invoke(16, Key);
        IvHash = hasher.Invoke(16, Iv);
    }
}