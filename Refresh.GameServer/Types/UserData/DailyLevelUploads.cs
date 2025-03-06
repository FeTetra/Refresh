namespace Refresh.GameServer.Types.UserData;

public partial class DailyLevelUploads
{
    public int Count { get; set; }

    public bool DateIsExpired { get; set; }

    public DateTimeOffset ExpiryDate { get; set; }
}