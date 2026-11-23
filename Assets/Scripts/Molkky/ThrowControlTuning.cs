using System;
using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 投げる操作の調整値。フェーズ2で実機を触りながら決める。
    /// スワイプの速さの単位は「画面の短い辺 / 秒」（端末の解像度に左右されないように）。
    /// </summary>
    [Serializable]
    public class ThrowControlTuning
    {
        [Header("スワイプ")]
        [Tooltip("指を離す直前のこの秒数の動きから速さと向きを求める")]
        public float swipeWindow = 0.1f;
        [Tooltip("これより遅いスワイプは投げない（短辺/秒）")]
        public float minSwipeSpeed = 0.4f;
        [Tooltip("真上からこの角度を超えて横に向いたスワイプは投げない（度）")]
        public float maxSwipeAngle = 60f;

        [Header("力")]
        [Tooltip("スワイプの速さ(短辺/秒) → 棒の初速(m/s) の倍率")]
        public float speedPerSwipe = 2.5f;
        [Tooltip("初速の上限 m/s")]
        public float maxThrowSpeed = 9f;

        [Header("向き")]
        [Tooltip("スワイプの角度(度) → 投げる向き(度) の倍率")]
        public float yawPerSwipeDeg = 0.35f;
        [Tooltip("投げる向きの上限（度）")]
        public float maxYawDeg = 12f;
        [Tooltip("立ち位置を動かしても、まっすぐのスワイプがピンの方へ向くようにする（カメラもピンを向く）")]
        public bool faceTarget = true;

        [Header("投げ方")]
        [Tooltip("縦投げの仰角（度）")]
        public float verticalElevationDeg = 18f;
        [Tooltip("横投げの仰角（度）")]
        public float horizontalElevationDeg = 18f;
        [Tooltip("縦投げの回転: 初速 1m/s あたり rad/s")]
        public float verticalSpinPerSpeed = 1f;
        [Tooltip("横投げの回転: 初速 1m/s あたり rad/s")]
        public float horizontalSpinPerSpeed = 1f;
        [Tooltip("手を離す高さ m")]
        public float releaseHeight = 0.6f;
        [Tooltip("投げるラインから手を離す位置までの前方距離 m")]
        public float releaseForward = 0.3f;

        [Header("立ち位置")]
        [Tooltip("投げるラインの中心から左右に動ける幅 m")]
        public float stanceRange = 0.5f;
        [Tooltip("左右に動く速さ m/s")]
        public float stanceMoveSpeed = 0.5f;

        [Header("カメラ")]
        public float cameraHeight = 1.5f;
        [Tooltip("投げるラインから後ろへの距離 m")]
        public float cameraBack = 1.2f;
        public float cameraFov = 40f;

        [Header("流れ")]
        [Tooltip("止まってから次に投げられるようになるまでの秒数")]
        public float resultHoldTime = 1.0f;

        public ThrowControlTuning Clone() => (ThrowControlTuning)MemberwiseClone();
    }
}
