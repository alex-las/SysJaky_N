using SysJaky_N.Models;

namespace SysJaky_N.Services;

public interface IAltchaService
{
    AltchaChallenge CreateChallenge();
    bool Verify(AltchaVerifyPayload payload);
    Task<bool> VerifySolutionAsync(string payload);
}
