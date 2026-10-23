using System.Collections.Generic;

namespace GameModes
{
    /**
     * What a game is being made as: a game mode (null for a game started
     * alone without one), played alone or with others. Settings say which
     * kinds of game they apply to (see Setting.When), and records and the
     * New Game page turn into one.
     */
    public readonly struct GameKind
    {
        public string Mode { get; }
        public bool Alone { get; }

        public GameKind(string mode, bool alone)
        {
            Mode = mode;
            Alone = alone;
        }

        public bool WithOthers => !Alone;

        public bool Is(string mode) => Mode == mode;

        /** The game a record is of: its mode, and whether it was played alone. */
        public static GameKind Of(IReadOnlyDictionary<string, string> record) => new(Modes.ModeOf(record), Modes.IsAlone(record));

        /** What the New Game page is making now: the mode picked, alone or with others. */
        public static GameKind OnPage
        {
            get
            {
                bool alone = Modes.PageAlone();
                return new GameKind(Modes.PickedMode(alone), alone);
            }
        }

        /** The new game loading on this machine, or the saved one's kind where known. */
        public static GameKind Loading => new(Modes.Game, Modes.GameAlone);

        /** The kind as one key for logs: the mode's id with others, "bb.alone:" and its id alone ("bb.alone" for no mode). */
        public string Key => Alone ? (Mode == null ? "bb.alone" : "bb.alone:" + Mode) : Mode;

        public override string ToString() => (Mode ?? "no mode") + (Alone ? " alone" : " with others");
    }
}
