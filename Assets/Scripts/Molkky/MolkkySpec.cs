using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 道具の寸法と配置（本物に合わせた固定値）。
    /// 端末ごとに結果が変わってはいけない値はここに置く。調整したい値は <see cref="PhysicsTuning"/> へ。
    /// </summary>
    public static class MolkkySpec
    {
        public const int PinCount = 12;

        /// <summary>記録データ上の並び: 0 = 棒、1〜12 = ピン番号</summary>
        public const int BodyCount = PinCount + 1;
        public const int StickBodyIndex = 0;

        public const float StickDiameter = 0.055f;
        public const float StickLength = 0.225f;

        public const float PinDiameter = 0.055f;
        public const float PinRadius = PinDiameter * 0.5f;
        /// <summary>高い側の高さ</summary>
        public const float PinHeight = 0.15f;
        /// <summary>斜めカットで低くなる側の高さ（低い側を投げる人に向ける）</summary>
        public const float PinCutLowHeight = 0.12f;

        public const float ThrowDistance = 3.5f;

        /// <summary>並べたときのピン同士の隙間。接したまま置くと最初から接触が発生して揺れるため。</summary>
        public const float PinGap = 0.001f;

        /// <summary>手前の列から（仕様書の並び）</summary>
        public static readonly int[][] PinRows =
        {
            new[] { 1, 2 },
            new[] { 3, 10, 4 },
            new[] { 5, 11, 12, 6 },
            new[] { 7, 9, 8 },
        };

        /// <summary>ピン番号の初期位置（底面の中心）。手前の列の中心が投げる距離。</summary>
        public static Vector3 InitialPinPosition(int pinNumber)
        {
            float pitch = PinDiameter + PinGap;
            float rowStep = pitch * 0.8660254f; // 隣り合うピンが接する三角形の並び
            for (int r = 0; r < PinRows.Length; r++)
            {
                var row = PinRows[r];
                for (int i = 0; i < row.Length; i++)
                {
                    if (row[i] != pinNumber) continue;
                    float startX = -(row.Length - 1) * pitch * 0.5f;
                    return new Vector3(startX + i * pitch, 0f, ThrowDistance + r * rowStep);
                }
            }
            throw new System.ArgumentOutOfRangeException(nameof(pinNumber));
        }

        // ---- 倒れた判定 ----
        // 端末ごとに同じ答えになるよう、姿勢（位置と回転）だけから決める。

        /// <summary>鉛直からこの角度以上傾いていること</summary>
        public const float FallenMinTiltDeg = 75f;

        /// <summary>中心の高さが「半径 + これ」以下であること（寄りかかっていると中心が高くなる）</summary>
        public const float FallenCenterHeightTolerance = 0.012f;

        /// <summary>ピンの軸の中央の高さ（原点は底面の中心）</summary>
        public static float PinCenterHeight(Vector3 position, Quaternion rotation)
        {
            return (position + rotation * Vector3.up * (PinHeight * 0.5f)).y;
        }

        public static float PinTiltDeg(Quaternion rotation)
        {
            return Vector3.Angle(rotation * Vector3.up, Vector3.up);
        }

        /// <summary>
        /// 地面に完全に倒れているか。他のピンや棒に寄りかかっているものは倒れていない扱い。
        /// </summary>
        public static bool IsPinFallen(Vector3 position, Quaternion rotation, float groundHeight = 0f)
        {
            if (PinTiltDeg(rotation) < FallenMinTiltDeg) return false;
            return PinCenterHeight(position, rotation) - groundHeight <= PinRadius + FallenCenterHeightTolerance;
        }
    }
}
