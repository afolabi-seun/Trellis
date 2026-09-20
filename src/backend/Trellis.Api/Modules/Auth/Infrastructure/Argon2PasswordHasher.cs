using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using Trellis.Api.Modules.Auth.Application.Interfaces;

namespace Trellis.Api.Modules.Auth.Infrastructure;

/// <summary>
/// security_guide.md's Argon2id parameters (OWASP Password Storage Cheat Sheet baseline):
/// 19 MiB memory, 2 iterations, degree of parallelism 1.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int MemorySizeKib = 19 * 1024;
    private const int Iterations = 2;
    private const int DegreeOfParallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split(':', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = ComputeHash(password, salt);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySizeKib,
        };

        return argon2.GetBytes(HashSize);
    }
}
