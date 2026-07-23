namespace left_4_bot.Persistence;

public readonly record struct CommandRewardResult(
    CommandRewardStatus Status,
    int Balance,
    int RewardsUsedToday,
    int TabsGrantedToday);
