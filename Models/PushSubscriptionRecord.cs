using System.Collections.Generic;

namespace SysJaky_N.Models;

public class PushSubscriptionRecord
{
    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public HashSet<string> Topics { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
