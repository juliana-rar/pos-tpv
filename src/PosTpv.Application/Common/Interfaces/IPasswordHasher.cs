namespace PosTpv.Application.Common.Interfaces;

/// <summary>Hashes and verifies user passwords/PINs.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
