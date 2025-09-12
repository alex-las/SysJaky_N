namespace SysJaky_N.Services;

public interface IAltchaService
{
    Task<bool> VerifySolutionAsync(string payload);
}
