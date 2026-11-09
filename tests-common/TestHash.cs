using System.Security.Cryptography;
namespace DobleCommander;
public static class TestFiles {
 public static async Task<byte[]> Hash(string path,CancellationToken token){await using var input=File.OpenRead(path);return await SHA256.HashDataAsync(input,token);}
}
