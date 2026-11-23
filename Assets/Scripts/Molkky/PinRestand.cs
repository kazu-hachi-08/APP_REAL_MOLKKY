using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 投げの後にピンを立て直す位置を決める。
    /// 端末ごとに同じ答えになるよう、記録の最終姿勢だけから決める（物理は使わない）。
    /// <list type="bullet">
    /// <item>立っているピン: その場で傾きだけ直す（向きはそのまま）</item>
    /// <item>倒れたピン・寄りかかったピン: ピンの中心の真下にまっすぐ立てる（斜めカットは投げる人に向ける）</item>
    /// <item>他のピンと重なる場合は、立て直したピンの方を少しずらす</item>
    /// </list>
    /// </summary>
    public static class PinRestand
    {
        /// <summary>これ以上傾いていたら立て直す（それ未満は立っている扱いで、その場で傾きだけ直す）</summary>
        public const float RestandTiltDeg = 10f;

        /// <summary>ずらすときのピン同士の隙間</summary>
        public const float SeparationGap = 0.003f;

        /// <summary>空いた場所を探す刻み m と、探す範囲（刻み×回数）</summary>
        const float SearchStep = 0.005f;
        const int SearchRings = 200;

        /// <summary>記録の最終フレームから立て直す姿勢を求める。配列の [0] = 1番</summary>
        public static void FromRecord(ThrowRecord record, Rect groundXZ, float groundY,
            Vector3[] outPositions, Quaternion[] outRotations, bool[] outRestood = null)
        {
            var pos = new Vector3[MolkkySpec.PinCount];
            var rot = new Quaternion[MolkkySpec.PinCount];
            int last = record.FrameCount - 1;
            for (int i = 0; i < MolkkySpec.PinCount; i++) record.GetPose(last, i + 1, out pos[i], out rot[i]);
            Compute(pos, rot, groundXZ, groundY, outPositions, outRotations, outRestood);
        }

        /// <param name="positions">今のピンの位置（底面の中心）。[0] = 1番</param>
        /// <param name="groundXZ">地面の範囲（x, z）。外に出たピンはこの中に戻す</param>
        /// <param name="groundY">地面の高さ</param>
        public static void Compute(Vector3[] positions, Quaternion[] rotations, Rect groundXZ, float groundY,
            Vector3[] outPositions, Quaternion[] outRotations, bool[] outRestood = null)
        {
            int n = MolkkySpec.PinCount;
            var xz = new Vector2[n];
            var movable = new bool[n];

            float inset = MolkkySpec.PinRadius;
            var area = Rect.MinMaxRect(groundXZ.xMin + inset, groundXZ.yMin + inset, groundXZ.xMax - inset, groundXZ.yMax - inset);

            for (int i = 0; i < n; i++)
            {
                var p = positions[i];
                var q = rotations[i];
                if (MolkkySpec.PinTiltDeg(q) < RestandTiltDeg)
                {
                    // 立っている: 位置と向き（鉛直軸回り）はそのまま
                    xz[i] = new Vector2(p.x, p.z);
                    var fwd = q * Vector3.forward;
                    fwd.y = 0f;
                    outRotations[i] = fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;
                }
                else
                {
                    // 倒れた: ピンの中心（軸の中央）の真下に立てる
                    var c = p + q * Vector3.up * (MolkkySpec.PinHeight * 0.5f);
                    xz[i] = new Vector2(c.x, c.z);
                    outRotations[i] = Quaternion.identity;
                    movable[i] = true;
                }

                // 地面の外に出た物は範囲内に戻す（立っている物も念のため）
                if (!area.Contains(xz[i]))
                {
                    xz[i] = new Vector2(Mathf.Clamp(xz[i].x, area.xMin, area.xMax), Mathf.Clamp(xz[i].y, area.yMin, area.yMax));
                    movable[i] = true;
                }
            }

            Separate(xz, movable, area);

            for (int i = 0; i < n; i++)
            {
                outPositions[i] = new Vector3(xz[i].x, groundY, xz[i].y);
                if (outRestood != null) outRestood[i] = movable[i];
            }
        }

        /// <summary>
        /// 重なりを解く。動かせるのは立て直したピンだけで、番号順に1本ずつ置く。
        /// 置きたい場所が空いていなければ、周りを近い順に探して最初に空いていた場所に置く
        /// （決まった順で探すので結果は端末間で揃う）。
        /// </summary>
        static void Separate(Vector2[] xz, bool[] movable, Rect area)
        {
            int n = xz.Length;
            var placed = new bool[n];
            for (int i = 0; i < n; i++) placed[i] = !movable[i];

            for (int i = 0; i < n; i++)
            {
                if (!movable[i]) continue;
                xz[i] = FindFreeSpot(xz[i], xz, placed, area);
                placed[i] = true;
            }
        }

        static Vector2 FindFreeSpot(Vector2 desired, Vector2[] xz, bool[] placed, Rect area)
        {
            if (IsFree(desired, xz, placed)) return desired;
            for (int ring = 1; ring <= SearchRings; ring++)
            {
                float radius = ring * SearchStep;
                int count = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / SearchStep));
                for (int k = 0; k < count; k++)
                {
                    float a = k * (2f * Mathf.PI / count);
                    var c = desired + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    if (!area.Contains(c)) continue;
                    if (IsFree(c, xz, placed)) return c;
                }
            }
            return desired; // 見つからない（ありえない広さ）ならそのまま
        }

        static bool IsFree(Vector2 p, Vector2[] xz, bool[] placed)
        {
            float minDist = MolkkySpec.PinDiameter + SeparationGap;
            float minSq = minDist * minDist;
            for (int j = 0; j < xz.Length; j++)
            {
                if (placed[j] && (xz[j] - p).sqrMagnitude < minSq) return false;
            }
            return true;
        }
    }
}
