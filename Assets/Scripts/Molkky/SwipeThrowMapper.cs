using UnityEngine;

namespace RealMolkky.Molkky
{
    public enum SwipeReject
    {
        None,
        TooSlow,
        NotForward,
        TooSideways,
    }

    /// <summary>
    /// スワイプの速度を投げる条件に変える。速さ → 力、向き → 方向。狙いの補助はしない。
    /// </summary>
    public static class SwipeThrowMapper
    {
        /// <summary>ピンの並びの中心（狙いの基準。カメラもここを向く）</summary>
        public static Vector3 PinsCenter => new Vector3(0f, 0f, MolkkySpec.ThrowDistance + 0.08f);

        /// <summary>立ち位置からピンの方を向くための左右の角度（度、+で右）</summary>
        public static float FacingYawDeg(float lateral, ThrowControlTuning t)
        {
            if (!t.faceTarget) return 0f;
            float dz = PinsCenter.z - t.releaseForward;
            return Mathf.Atan2(PinsCenter.x - lateral, dz) * Mathf.Rad2Deg;
        }

        /// <param name="releaseVelocity">離す直前の画面上の速度（短辺/秒、上が +y）。力に使う</param>
        /// <param name="stroke">スワイプ全体の動き（<see cref="SwipeTracker.StrokeVector"/>）。向きに使う。
        /// ゼロなら離す直前の速度の向きを使う</param>
        public static SwipeReject TryMap(Vector2 releaseVelocity, Vector2 stroke, ThrowStyle style, float lateral,
            ThrowControlTuning t, out ThrowParams p)
        {
            p = default;
            float swipeSpeed = releaseVelocity.magnitude;
            if (swipeSpeed < t.minSwipeSpeed) return SwipeReject.TooSlow;
            if (releaseVelocity.y <= 0f) return SwipeReject.NotForward;

            var dir = stroke.sqrMagnitude > 1e-8f ? stroke : releaseVelocity;
            if (dir.y <= 0f) return SwipeReject.NotForward;
            float angle = SwipeAngleDeg(dir);
            if (Mathf.Abs(angle) > t.maxSwipeAngle) return SwipeReject.TooSideways;

            bool vertical = style == ThrowStyle.Vertical;
            float speed = ThrowSpeed(swipeSpeed, style, t);
            float yaw = Mathf.Clamp(angle * t.yawPerSwipeDeg, -t.maxYawDeg, t.maxYawDeg);

            p = new ThrowParams
            {
                style = style,
                speed = speed,
                elevationDeg = vertical ? t.verticalElevationDeg : t.horizontalElevationDeg,
                yawDeg = FacingYawDeg(lateral, t) + yaw,
                spin = speed * (vertical ? t.verticalSpinPerSpeed : t.horizontalSpinPerSpeed),
                lateral = lateral,
                releaseHeight = t.releaseHeight,
                releaseForward = t.releaseForward,
            };
            return SwipeReject.None;
        }

        /// <summary>
        /// スワイプの速さ → 初速。基準の速さで基準の初速になり、そこからのずれは小さめに効かせる
        /// （速さの比例にすると、スワイプの速さが数%ずれるだけで届く距離が大きく変わるため）。
        /// </summary>
        public static float ThrowSpeed(float swipeSpeed, ThrowStyle style, ThrowControlTuning t)
        {
            float baseSpeed = style == ThrowStyle.Vertical ? t.verticalBaseSpeed : t.horizontalBaseSpeed;
            float v = baseSpeed + (swipeSpeed - t.referenceSwipeSpeed) * t.speedPerSwipe;
            return Mathf.Clamp(v, t.minThrowSpeed, t.maxThrowSpeed);
        }

        /// <summary>真上を0、右を+とした角度（度）</summary>
        public static float SwipeAngleDeg(Vector2 v) => Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;

        /// <summary>投げる前に手に持っている棒の姿勢（手を離す位置・向きと同じ）</summary>
        public static void HoldPose(ThrowStyle style, float lateral, ThrowControlTuning t,
            out Vector3 position, out Quaternion rotation)
        {
            var p = new ThrowParams
            {
                style = style,
                yawDeg = FacingYawDeg(lateral, t),
                lateral = lateral,
                releaseHeight = t.releaseHeight,
                releaseForward = t.releaseForward,
            };
            ThrowSimulator.ReleasePose(p, out position, out rotation, out _, out _);
        }
    }
}
