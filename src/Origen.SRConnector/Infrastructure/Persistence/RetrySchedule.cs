namespace Origen.SRConnector.Infrastructure.Persistence;

public static class RetrySchedule
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1)
    ];

    public static TimeSpan ForAttempt(int attempts)
    {
        var index = Math.Clamp(attempts - 1, 0, Delays.Length - 1);
        return Delays[index];
    }
}

