using System;
using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 投げる操作の調整値。フェーズ2で実機を触りながら決める。
    /// スワイプの速さの単位は「1000 CSSピクセル / 秒」（kpx/s）。CSSピクセルは端末によらず
    /// だいたい同じ物理的な長さなので、スマホとPCで同じ手の速さがほぼ同じ値になる。
    /// </summary>
    [Serializable]
    public class ThrowControlTuning
    {
        [Header("スワイプ")]
        [Tooltip("指を離す直前のこの秒数の動きから速さを求める（向きはスワイプ全体から求める）")]
        public float swipeWindow = 0.1f;
        [Tooltip("これより遅いスワイプは投げない（kpx/s）")]
        public float minSwipeSpeed = 0.3f;
        [Tooltip("真上からこの角度を超えて横に向いたスワイプは投げない（度）")]
        public float maxSwipeAngle = 60f;

        [Header("力")]
        [Tooltip("基準のスワイプの速さ（kpx/s）。この速さで投げると下の基準の初速になる")]
        public float referenceSwipeSpeed = 2.0f;
        [Tooltip("縦投げの基準の初速 m/s（ピンの並びに届く強さ）")]
        public float verticalBaseSpeed = 6.0f;
        [Tooltip("横投げの基準の初速 m/s。今の芝の材質では転がって当たっても倒れにくいので、低く飛んで当たる強さにしている")]
        public float horizontalBaseSpeed = 6.0f;
        [Tooltip("スワイプの速さが2倍になったときに初速が増える量 m/s（半分なら同じだけ減る）。" +
                 "倍率で効かせるので、基準が手の速さと多少ずれても極端に飛びすぎたり届かなかったりしない")]
        public float speedPerDoubling = 0.8f;
        [Tooltip("初速の下限 m/s")]
        public float minThrowSpeed = 2.5f;
        [Tooltip("初速の上限 m/s")]
        public float maxThrowSpeed = 9f;

        [Header("向き")]
        [Tooltip("スワイプの角度(度) → 投げる向き(度) の倍率。小さいほどスワイプの向きのずれに強い")]
        public float yawPerSwipeDeg = 0.15f;
        [Tooltip("投げる向きの上限（度）")]
        public float maxYawDeg = 8f;
        [Tooltip("立ち位置を動かしても、まっすぐのスワイプがピンの方へ向くようにする（カメラもピンを向く）")]
        public bool faceTarget = true;

        [Header("投げ方")]
        [Tooltip("縦投げの仰角（度）")]
        public float verticalElevationDeg = 18f;
        [Tooltip("横投げの仰角（度）")]
        public float horizontalElevationDeg = 18f;
        [Tooltip("縦投げの前回り: 初速 1m/s あたり rad/s。強すぎると着地の向きがばらばらになり横に跳ねる")]
        public float verticalSpinPerSpeed = 0.5f;
        [Tooltip("横投げの前回り（長い軸のまわり）: 初速 1m/s あたり rad/s。0 が一番まっすぐ転がる")]
        public float horizontalSpinPerSpeed = 0f;
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
