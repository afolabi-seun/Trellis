namespace Trellis.Api.Modules.Auth.Application.Interfaces;

/// <summary>security_guide.md's Argon2id password hashing.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
