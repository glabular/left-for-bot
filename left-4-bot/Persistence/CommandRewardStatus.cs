namespace left_4_bot.Persistence;

public enum CommandRewardStatus
{
    /// <summary>Not linked or starter not claimed via /balance.</summary>
    NotEligible,

    /// <summary>Already received CommandRewardsPerGamingDay rewards today.</summary>
    DailyLimitReached,

    /// <summary>Reward granted; do not spam the chat (1st or 2nd of the day).</summary>
    GrantedSilent,

    /// <summary>Third reward of the gaming day — show summary in chat.</summary>
    GrantedDailyComplete,
}
