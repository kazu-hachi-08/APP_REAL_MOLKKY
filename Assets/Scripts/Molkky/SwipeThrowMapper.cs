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

        /// <param name="swipeVelocity">画面上の速度（短辺/秒、上が +y）</param>
        public static SwipeReject TryMap(Vector2 swipeVelocity, ThrowStyle style, float lateral,
            ThrowControlTuning t, out ThrowParams p)
        {
            p = default;
            float swipeSpeed = swipeVelocity.magnitude;
            if (swipeSpeed < t.minSwipeSpeed) return SwipeReject.TooSlow;
            if (swipeVelocity.y <= 0f) return SwipeReject.NotForward;

            float angle = SwipeAngleDeg(swipeVelocity);
            if (Mathf.Abs(angle) > t.maxSwipeAngle) return SwipeReject.TooSideways;

            bool vertical = style == ThrowStyle.Vertical;
            float speed = Mathf.Min(swipeSpeed * t.speedPerSwipe, t.maxThrowSpeed);
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
