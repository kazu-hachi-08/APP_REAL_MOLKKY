using System.Collections.Generic;

namespace RealMolkky.Molkky
{
    public static class MolkkyScoring
    {
        /// <summary>1本だけ倒れたらその番号の点、2本以上なら倒れた本数。</summary>
        public static int ThrowScore(IReadOnlyList<int> fallenPinNumbers)
        {
            if (fallenPinNumbers == null || fallenPinNumbers.Count == 0) return 0;
            if (fallenPinNumbers.Count == 1) return fallenPinNumbers[0];
            return fallenPinNumbers.Count;
        }

        /// <summary>記録の最終姿勢から倒れたピン番号を求める（各端末でこれを使う）。</summary>
        public static List<int> FallenPins(ThrowRecord record)
        {
            var result = new List<int>();
            int last = record.FrameCount - 1;
            for (int pin = 1; pin <= MolkkySpec.PinCount; pin++)
            {
                record.GetPose(last, pin, out var pos, out var rot);
                if (MolkkySpec.IsPinFallen(pos, rot)) result.Add(pin);
            }
            return result;
        }
    }
}
