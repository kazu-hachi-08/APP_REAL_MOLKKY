using System;
using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 物理の調整値。フェーズ1のサンドボックスで実機を見ながら決める。
    /// 投げた端末だけが物理を計算する方式なので、端末ごとに一致させる必要はない。
    /// </summary>
    [Serializable]
    public class PhysicsTuning
    {
        [Header("精度")]
        [Tooltip("物理の1ステップの秒数")]
        public float fixedDeltaTime = 1f / 120f;
        public int solverIterations = 12;
        public int solverVelocityIterations = 4;
        [Tooltip("ピンの寸法(5.5cm)に対して既定の1cmは大きすぎる")]
        public float contactOffset = 0.003f;
        [Tooltip("これより遅い衝突は跳ねない")]
        public float bounceThreshold = 0.8f;
        public float sleepThreshold = 0.005f;
        public float maxAngularVelocity = 60f;
        [Tooltip("1フレームで進める最大ステップ数。超えた分は遅れとして捨てる（スロー再生になる）")]
        public int maxStepsPerFrame = 8;

        [Header("重さ")]
        [Tooltip("白樺 約650kg/m3。ピン約0.2kg、棒約0.35kg になる")]
        public float woodDensity = 650f;

        [Header("木（棒とピン）")]
        public float woodStaticFriction = 0.5f;
        public float woodDynamicFriction = 0.4f;
        public float woodBounciness = 0.35f;

        [Header("地面（芝生）")]
        public float groundStaticFriction = 0.75f;
        public float groundDynamicFriction = 0.6f;
        public float groundBounciness = 0.12f;
        [Tooltip("芝の転がり抵抗。地面に触れている間だけ回転をこの割合(1/秒)で弱める")]
        public float groundRollingResistance = 3f;

        [Header("空気抵抗")]
        public float linearDamping = 0.02f;
        public float angularDamping = 0.05f;

        [Header("止まった判定")]
        public float stopLinearSpeed = 0.02f;
        public float stopAngularSpeed = 0.25f;
        [Tooltip("この秒数だけ遅い状態が続いたら止まったとみなす")]
        public float stopHoldTime = 0.4f;
        [Tooltip("止まらないときの打ち切り時間（秒）")]
        public float simulationTimeout = 12f;

        [Header("記録")]
        [Tooltip("何ステップごとに姿勢を残すか")]
        public int recordEverySteps = 4;

        public PhysicsTuning Clone() => (PhysicsTuning)MemberwiseClone();
    }
}
