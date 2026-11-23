using System.Runtime.InteropServices;
using UnityEngine;

namespace RealMolkky.Play
{
    /// <summary>
    /// 画面のピクセルを CSS ピクセルに直す倍率。
    /// CSS ピクセルは端末によらずだいたい同じ物理的な長さなので、スワイプの速さをスマホとPCで比べられる。
    /// （画面の大きさに対する割合で測ると、スマホは画面が小さいぶん同じ手の速さでも約3倍速く読まれる）
    /// </summary>
    public static class ScreenScale
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern float RealMolkky_CanvasPixelRatio();
#endif

        /// <summary>1 CSS ピクセルあたりの画面のピクセル数</summary>
        public static float PixelsPerCssPixel
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                float r = RealMolkky_CanvasPixelRatio();
                return r > 0.1f ? r : 1f;
#else
                return 1f;
#endif
            }
        }
    }
}
