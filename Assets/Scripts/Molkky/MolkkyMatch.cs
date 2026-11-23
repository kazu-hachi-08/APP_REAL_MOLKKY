using System;
using System.Collections.Generic;

namespace RealMolkky.Molkky
{
    public enum MatchMode
    {
        /// <summary>個人戦: 1人が1つの陣営</summary>
        Individual,
        /// <summary>チーム戦: 2人×2チーム。得点と失格はチーム単位、メンバーが交互に投げる</summary>
        Team,
    }

    /// <summary>得点を持つ単位（個人戦は1人、チーム戦は1チーム）</summary>
    public sealed class MatchSide
    {
        public string Name { get; }
        public IReadOnlyList<string> Members => _members;
        public int Score { get; internal set; }
        /// <summary>連続で0点だった回数</summary>
        public int Misses { get; internal set; }
        public bool Disqualified { get; internal set; }
        /// <summary>次に投げるメンバー</summary>
        public int NextMemberIndex { get; internal set; }
        public string NextMember => _members[NextMemberIndex];

        readonly List<string> _members;

        internal MatchSide(string name, List<string> members)
        {
            Name = name;
            _members = members;
        }
    }

    /// <summary>1回の投げを反映した結果（表示用）</summary>
    public struct TurnResult
    {
        public int sideIndex;
        public string thrower;
        public int points;
        public int scoreBefore;
        public int scoreAfter;
        /// <summary>50点を超えて25点に戻った</summary>
        public bool overflow;
        /// <summary>0点だった</summary>
        public bool missed;
        /// <summary>この投げの後の連続0点の回数</summary>
        public int misses;
        public bool disqualified;
        /// <summary>この投げで試合が終わった</summary>
        public bool matchOver;
        /// <summary>勝った陣営（いなければ -1）</summary>
        public int winnerIndex;
    }

    /// <summary>
    /// 1試合のルール（物理や画面とは独立）。
    /// 50点ちょうどで勝ち、超えたら25点に戻る、3回連続0点で失格。
    /// 投げる順番は試合開始時に乱数の種から決める（オンラインでは種を共有すれば全端末で同じ順番になる）。
    /// </summary>
    public sealed class MolkkyMatch
    {
        public const int TargetScore = 50;
        public const int OverflowResetScore = 25;
        public const int MaxConsecutiveMisses = 3;
        public const int MaxPlayers = 4;
        public const int TeamSize = 2;
        public const int TeamCount = 2;

        public MatchMode Mode { get; }
        public int Seed { get; }
        /// <summary>投げる順番に並んだ陣営</summary>
        public IReadOnlyList<MatchSide> Sides => _sides;
        public int CurrentSideIndex { get; private set; }
        public MatchSide CurrentSide => _sides[CurrentSideIndex];
        public string CurrentThrower => CurrentSide.NextMember;
        public bool IsOver { get; private set; }
        /// <summary>勝った陣営（終わっていない・勝者なしなら -1）</summary>
        public int WinnerIndex { get; private set; } = -1;
        /// <summary>これまでに投げた回数</summary>
        public int ThrowCount { get; private set; }

        readonly List<MatchSide> _sides = new List<MatchSide>();

        /// <param name="playerNames">
        /// 個人戦: 1〜4人。チーム戦: 4人で、前の2人がチーム1、後ろの2人がチーム2。
        /// </param>
        public MolkkyMatch(MatchMode mode, IReadOnlyList<string> playerNames, int seed)
        {
            if (playerNames == null) throw new ArgumentNullException(nameof(playerNames));
            Mode = mode;
            Seed = seed;
            var rng = new Random(seed);

            if (mode == MatchMode.Individual)
            {
                if (playerNames.Count < 1 || playerNames.Count > MaxPlayers)
                    throw new ArgumentException($"個人戦は1〜{MaxPlayers}人です。", nameof(playerNames));
                foreach (var n in playerNames) _sides.Add(new MatchSide(n, new List<string> { n }));
            }
            else
            {
                if (playerNames.Count != TeamSize * TeamCount)
                    throw new ArgumentException($"チーム戦は{TeamSize * TeamCount}人です。", nameof(playerNames));
                for (int t = 0; t < TeamCount; t++)
                {
                    var members = new List<string>();
                    for (int m = 0; m < TeamSize; m++) members.Add(playerNames[t * TeamSize + m]);
                    Shuffle(members, rng); // チームの中で誰から投げるか
                    _sides.Add(new MatchSide($"Team {t + 1}", members));
                }
            }

            Shuffle(_sides, rng); // どの陣営から投げるか
            CurrentSideIndex = 0;
        }

        static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>今の番の人の1回の投げの得点を反映し、次の番へ進める</summary>
        public TurnResult ApplyThrow(int points)
        {
            if (IsOver) throw new InvalidOperationException("試合は終わっています。");
            if (points < 0 || points > MolkkySpec.PinCount) throw new ArgumentOutOfRangeException(nameof(points));

            var side = CurrentSide;
            var r = new TurnResult
            {
                sideIndex = CurrentSideIndex,
                thrower = side.NextMember,
                points = points,
                scoreBefore = side.Score,
                winnerIndex = -1,
            };

            ThrowCount++;
            if (points == 0)
            {
                side.Misses++;
                r.missed = true;
                if (side.Misses >= MaxConsecutiveMisses)
                {
                    side.Disqualified = true;
                    r.disqualified = true;
                }
            }
            else
            {
                side.Misses = 0;
                int s = side.Score + points;
                if (s > TargetScore)
                {
                    s = OverflowResetScore;
                    r.overflow = true;
                }
                side.Score = s;
            }
            r.scoreAfter = side.Score;
            r.misses = side.Misses;

            // チームはメンバーが交互に投げる
            side.NextMemberIndex = (side.NextMemberIndex + 1) % side.Members.Count;

            if (side.Score == TargetScore)
            {
                Finish(CurrentSideIndex);
            }
            else
            {
                int active = 0, lastActive = -1;
                for (int i = 0; i < _sides.Count; i++)
                {
                    if (_sides[i].Disqualified) continue;
                    active++;
                    lastActive = i;
                }
                // 他が全員失格したら残った1人（1チーム）の勝ち。1人で遊んでいて失格したら勝者なしで終わり
                if (active == 0) Finish(-1);
                else if (active == 1 && _sides.Count > 1) Finish(lastActive);
                else AdvanceTurn();
            }

            r.matchOver = IsOver;
            r.winnerIndex = WinnerIndex;
            return r;
        }

        void Finish(int winner)
        {
            IsOver = true;
            WinnerIndex = winner;
        }

        void AdvanceTurn()
        {
            for (int k = 1; k <= _sides.Count; k++)
            {
                int i = (CurrentSideIndex + k) % _sides.Count;
                if (_sides[i].Disqualified) continue;
                CurrentSideIndex = i;
                return;
            }
        }
    }
}
