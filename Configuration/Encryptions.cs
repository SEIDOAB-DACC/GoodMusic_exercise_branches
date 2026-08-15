using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

using Configuration.Options;

namespace Configuration;

public class Encryptions
{
    private readonly AesEncryptionOptions _aesOption;

    public Encryptions(IOptions<AesEncryptionOptions> aesOptions)
    {
        _aesOption = aesOptions.Value;
        _aesOption.HashKeyIv(Pbkdf2HashToBytes);
    }

    public string AesEncryptToBase64<T>(T sourceToEncrypt)
    {
        string stringToEncrypt = JsonConvert.SerializeObject(sourceToEncrypt);
        byte[] dataset = System.Text.Encoding.Unicode.GetBytes(stringToEncrypt);

        //Encrypt using AES
        byte[] encryptedBytes;
        using (SymmetricAlgorithm algorithm = Aes.Create())
        using (ICryptoTransform encryptor = algorithm.CreateEncryptor(_aesOption.KeyHash, _aesOption.IvHash))
        {
            encryptedBytes = encryptor.TransformFinalBlock(dataset, 0, dataset.Length);
        }

        return Convert.ToBase64String(encryptedBytes);
    }

    public T AesDecryptFromBase64<T>(string encryptedBase64)
    {
        byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);

        byte[] decryptedBytes;
        using (SymmetricAlgorithm algorithm = Aes.Create())
        using (ICryptoTransform decryptor = algorithm.CreateDecryptor(_aesOption.KeyHash, _aesOption.IvHash))
        {
            decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        }

        string decryptedString = System.Text.Encoding.Unicode.GetString(decryptedBytes);
        T decryptedObject = JsonConvert.DeserializeObject<T>(decryptedString);

        return decryptedObject;
    }

    private byte[] Pbkdf2HashToBytes (int nrBytes, string password)
    {
        byte[] registeredPasswordKeyDerivation = KeyDerivation.Pbkdf2(
            password: password,
            salt: Encoding.UTF8.GetBytes(_aesOption.Salt),
            prf: KeyDerivationPrf.HMACSHA512,
            iterationCount: _aesOption.Iterations,
            numBytesRequested: nrBytes);

        return registeredPasswordKeyDerivation;
    }

    public string EncryptPasswordToBase64(string password)
    {
        //Hash a password using salt and streching
        byte[] encrypted = Pbkdf2HashToBytes(64, password);
        return Convert.ToBase64String(encrypted);
    }
}