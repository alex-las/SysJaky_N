namespace SysJaky_N.Services;

public class WaitlistOptions
{
    public string PublicBaseUrl { get; set; } = "https://localhost";
    public int PollIntervalSeconds { get; set; } = 60;
    public string ClaimPath { get; set; } = "/api/waitlist/claim";
}
