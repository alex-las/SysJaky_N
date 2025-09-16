using SysJaky_N.Models;

namespace SysJaky_N.Services;

public interface IAltchaService
{
    AltchaChallenge CreateChallenge();
    bool Verify(AltchaProof proof);

    // Add the missing method definition to resolve CS1061
    Task<bool> VerifySolutionAsync(string payload);
}
