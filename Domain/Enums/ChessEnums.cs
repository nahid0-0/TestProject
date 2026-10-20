namespace Chess.Domain.Enums;

public enum Color { White, Black }
public enum GameResult { WhiteWin, BlackWin, Draw, Unknown }
public enum Termination { Checkmate, Resign, Timeout, Stalemate, Agreement, Abandoned, Other }
public enum GamePhase { Opening, Middlegame, Endgame }
public enum MoveClassification { Best, Good, Inaccuracy, Mistake, Blunder }
public enum SyncStatus { Pending, Running, Succeeded, Failed, Partial }
public enum SyncMode { Backfill, Incremental, Manual }
public enum TimeControlCategory { UltraBullet, Bullet, Blitz, Rapid, Classical, Correspondence }

[Flags]
public enum EnabledModules
{
    None = 0,
    RatingTrend = 1,
    WinLoss = 2,
    Openings = 4,
    TimeManagement = 8,
    Accuracy = 16,
    PhaseStrength = 32,
    All = RatingTrend | WinLoss | Openings | TimeManagement | Accuracy | PhaseStrength
}
