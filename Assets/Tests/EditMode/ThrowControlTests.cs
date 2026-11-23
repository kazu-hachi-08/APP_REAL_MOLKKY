using System.Collections.Generic;
using NUnit.Framework;
using RealMolkky.Molkky;
using UnityEngine;

namespace RealMolkky.Tests
{
    public class SwipeTrackerTests
    {
        [Test]
        public void Velocity_UsesOnlyTheLastWindow()
        {
            var tr = new SwipeTracker();
            // 最初はゆっくり、最後の0.1秒だけ速く動かす
            tr.Begin(0.0, new Vector2(0f, 0f));
            for (int i = 1; i <= 30; i++) tr.Add(i / 60.0, new Vector2(0f, i * 1f));      // 60/s
            for (int i = 1; i <= 6; i++) tr.Add(0.5 + i / 60.0, new Vector2(0f, 30f + i * 10f)); // 600/s
            tr.End(0.6, tr.LastPosition);

            var v = tr.VelocityAtEnd(0.1f);
            Assert.That(v.y, Is.EqualTo(600f).Within(1f));
            Assert.That(v.x, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void ReleaseWithoutMovement_DoesNotSlowDown()
        {
            var tr = new SwipeTracker();
            tr.Begin(0.0, Vector2.zero);
            for (int i = 1; i <= 6; i++) tr.Add(i / 60.0, new Vector2(i * 5f, i * 10f));
            tr.End(7 / 60.0, tr.LastPosition); // 離すフレームは動いていない

            var v = tr.VelocityAtEnd(0.1f);
            Assert.That(v.x, Is.EqualTo(300f).Within(1f));
            Assert.That(v.y, Is.EqualTo(600f).Within(1f));
        }

        [Test]
        public void SlowFrames_FallBackToPreviousSample()
        {
            // 1フレームが窓より長くても、1つ前のサンプルとの差で求める
            var tr = new SwipeTracker();
            tr.Begin(0.0, Vector2.zero);
            tr.Add(0.25, new Vector2(0f, 100f));
            tr.End(0.25, tr.LastPosition);
            Assert.That(tr.VelocityAtEnd(0.1f).y, Is.EqualTo(400f).Within(1e-2f));
        }

        [Test]
        public void Stroke_IgnoresWindUp_AndIsStraighterThanTheEnd()
        {
            // 先に少し下へ引き、まっすぐ上へ動かして、最後だけ右へ曲がる（親指の弧）
            var tr = new SwipeTracker();
            tr.Begin(0.0, new Vector2(0f, 0f));
            tr.Add(0.05, new Vector2(-10f, -30f)); // 引き（含めない）
            for (int i = 1; i <= 10; i++) tr.Add(0.05 + i / 60.0, new Vector2(-10f, -30f + i * 30f));
            tr.Add(0.25, new Vector2(10f, 290f));  // 最後に右へ
            tr.End(0.26, new Vector2(30f, 320f));

            var stroke = tr.StrokeVector();
            Assert.That(stroke.x, Is.EqualTo(40f).Within(1e-3f));
            Assert.That(stroke.y, Is.EqualTo(350f).Within(1e-3f));
            // 離す直前だけ見ると大きく右に向いている
            var v = tr.VelocityAtEnd(0.03f);
            Assert.Greater(SwipeThrowMapper.SwipeAngleDeg(v), 2f * SwipeThrowMapper.SwipeAngleDeg(stroke));
        }

        [Test]
        public void SingleSample_IsZero()
        {
            var tr = new SwipeTracker();
            tr.Begin(0.0, Vector2.one);
            tr.End(0.0, Vector2.one);
            Assert.AreEqual(Vector2.zero, tr.VelocityAtEnd(0.1f));
            Assert.AreEqual(Vector2.zero, tr.StrokeVector());
        }
    }

    public class SwipeThrowMapperTests
    {
        readonly ThrowControlTuning _t = new ThrowControlTuning();

        static SwipeReject Map(Vector2 v, ThrowStyle style, float lateral, ThrowControlTuning t, out ThrowParams p)
            => SwipeThrowMapper.TryMap(v, v, style, lateral, t, out p);

        [Test]
        public void ReferenceSwipe_GivesBaseSpeed()
        {
            var v = new Vector2(0f, _t.referenceSwipeSpeed);
            Assert.AreEqual(SwipeReject.None, Map(v, ThrowStyle.Vertical, 0f, _t, out var p));
            Assert.That(p.speed, Is.EqualTo(_t.verticalBaseSpeed).Within(1e-4f));
            Assert.That(p.yawDeg, Is.EqualTo(0f).Within(1e-4f));
            Assert.AreEqual(ThrowStyle.Vertical, p.style);
            Assert.That(p.elevationDeg, Is.EqualTo(_t.verticalElevationDeg));
            Assert.That(p.spin, Is.EqualTo(p.speed * _t.verticalSpinPerSpeed).Within(1e-4f));

            Map(v, ThrowStyle.Horizontal, 0f, _t, out p);
            Assert.That(p.speed, Is.EqualTo(_t.horizontalBaseSpeed).Within(1e-4f));
            Assert.That(p.elevationDeg, Is.EqualTo(_t.horizontalElevationDeg));
        }

        [Test]
        public void SwipeSpeedError_ChangesThrowSpeedOnlyALittle()
        {
            // スワイプの速さが10%ずれても、初速のずれは3%未満
            float r = _t.referenceSwipeSpeed;
            float a = SwipeThrowMapper.ThrowSpeed(r, ThrowStyle.Vertical, _t);
            float b = SwipeThrowMapper.ThrowSpeed(r * 1.1f, ThrowStyle.Vertical, _t);
            Assert.Greater(b, a);
            Assert.Less((b - a) / a, 0.03f);

            // 2倍・半分の速さでも、初速の変化は speedPerDoubling だけ
            float fast = SwipeThrowMapper.ThrowSpeed(r * 2f, ThrowStyle.Vertical, _t);
            float slow = SwipeThrowMapper.ThrowSpeed(r * 0.5f, ThrowStyle.Vertical, _t);
            Assert.That(fast - a, Is.EqualTo(_t.speedPerDoubling).Within(1e-3f));
            Assert.That(a - slow, Is.EqualTo(_t.speedPerDoubling).Within(1e-3f));
        }

        [Test]
        public void Speed_IsClamped()
        {
            Map(new Vector2(0f, 100f), ThrowStyle.Horizontal, 0f, _t, out var p);
            Assert.That(p.speed, Is.EqualTo(_t.maxThrowSpeed));
            var t = _t.Clone();
            t.speedPerDoubling = 10f;
            Map(new Vector2(0f, t.minSwipeSpeed), ThrowStyle.Vertical, 0f, t, out p);
            Assert.That(p.speed, Is.EqualTo(t.minThrowSpeed));
        }

        [Test]
        public void SwipeToTheRight_AimsRight_AndIsCapped()
        {
            Map(new Vector2(1f, 2f), ThrowStyle.Vertical, 0f, _t, out var p);
            Assert.Greater(p.yawDeg, 0f);
            float angle = Mathf.Atan2(1f, 2f) * Mathf.Rad2Deg;
            Assert.That(p.yawDeg, Is.EqualTo(Mathf.Min(angle * _t.yawPerSwipeDeg, _t.maxYawDeg)).Within(1e-3f));

            var t = _t.Clone();
            t.yawPerSwipeDeg = 1f;
            t.maxYawDeg = 5f;
            Map(new Vector2(-1f, 1f), ThrowStyle.Vertical, 0f, t, out p);
            Assert.That(p.yawDeg, Is.EqualTo(-5f).Within(1e-4f));
        }

        [Test]
        public void Direction_ComesFromStroke_PowerFromReleaseVelocity()
        {
            // 離す直前は右へ曲がっていても、スワイプ全体がまっすぐならまっすぐ投げる
            var release = new Vector2(1f, 2f);
            var stroke = new Vector2(0f, 0.5f);
            SwipeThrowMapper.TryMap(release, stroke, ThrowStyle.Vertical, 0f, _t, out var p);
            Assert.That(p.yawDeg, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(p.speed, Is.EqualTo(SwipeThrowMapper.ThrowSpeed(release.magnitude, ThrowStyle.Vertical, _t)).Within(1e-4f));
        }

        [Test]
        public void BadSwipes_AreRejected()
        {
            Assert.AreEqual(SwipeReject.TooSlow, Map(new Vector2(0f, 0.1f), ThrowStyle.Vertical, 0f, _t, out _));
            Assert.AreEqual(SwipeReject.NotForward, Map(new Vector2(0f, -2f), ThrowStyle.Vertical, 0f, _t, out _));
            Assert.AreEqual(SwipeReject.TooSideways, Map(new Vector2(2f, 0.3f), ThrowStyle.Vertical, 0f, _t, out _));
        }

        [Test]
        public void Stance_FacesPins()
        {
            // 左に立つと、まっすぐのスワイプはピンの方（右）へ向く
            Map(new Vector2(0f, 2f), ThrowStyle.Vertical, -0.5f, _t, out var p);
            Assert.That(p.lateral, Is.EqualTo(-0.5f));
            Assert.Greater(p.yawDeg, 0f);

            ThrowSimulator.ReleasePose(p, out var pos, out _, out var vel, out _);
            // 速度の延長線がピンの中心の近くを通る
            float tHit = (SwipeThrowMapper.PinsCenter.z - pos.z) / vel.z;
            Assert.That(pos.x + vel.x * tHit, Is.EqualTo(SwipeThrowMapper.PinsCenter.x).Within(0.01f));

            var t = _t.Clone();
            t.faceTarget = false;
            Map(new Vector2(0f, 2f), ThrowStyle.Vertical, -0.5f, t, out p);
            Assert.That(p.yawDeg, Is.EqualTo(0f).Within(1e-4f));
        }
    }

    public class TuningSerializationTests
    {
        /// <summary>
        /// 調整値がシーンに保存されると、古い値がコードの初期値より優先されて直した値が効かなくなる
        /// （実際に公開版で起きた）。保存されないことを確かめる。
        /// </summary>
        [Test]
        public void TuningFields_AreNotSavedInScenes()
        {
            var fields = new[]
            {
                typeof(RealMolkky.Play.ThrowController).GetField("tuning"),
                typeof(MolkkyRig).GetField("tuning"),
            };
            foreach (var f in fields)
            {
                Assert.IsNotNull(f);
                Assert.IsTrue(f.IsNotSerialized, $"{f.DeclaringType.Name}.{f.Name} is serialized");
            }
        }
    }

    /// <summary>標準的なスワイプで実際にピンまで届くか（物理を回す）</summary>
    public class SwipeThrowPhysicsTests
    {
        readonly List<Object> _created = new List<Object>();
        SimulationMode _prevMode;
        MolkkyRig _rig;
        ThrowSimulator _sim;

        [SetUp]
        public void SetUp()
        {
            _prevMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            var pinMesh = Track(MolkkyMeshFactory.CreatePin());
            var stickMesh = Track(MolkkyMeshFactory.CreateStick());
            var groundGo = Track(new GameObject("TestGround"));
            var ground = groundGo.AddComponent<BoxCollider>();
            ground.center = new Vector3(0f, -0.5f, 2f);
            ground.size = new Vector3(20f, 1f, 20f);
            var pins = new Rigidbody[MolkkySpec.PinCount];
            for (int i = 0; i < pins.Length; i++) pins[i] = Body($"TestPin_{i + 1}", pinMesh);
            var root = Track(new GameObject("TestRig"));
            _rig = root.AddComponent<MolkkyRig>();
            _rig.Bind(Body("TestStick", stickMesh), pins, ground);
            _sim = root.AddComponent<ThrowSimulator>();
            _sim.Bind(_rig);
            _rig.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            _created.Clear();
            Physics.simulationMode = _prevMode;
        }

        T Track<T>(T o) where T : Object
        {
            _created.Add(o);
            return o;
        }

        Rigidbody Body(string name, Mesh mesh)
        {
            var go = Track(new GameObject(name));
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;
            return go.AddComponent<Rigidbody>();
        }

        static ThrowParams MapReference(ThrowStyle style, float lateral, float swipeScale = 1f)
        {
            var t = new ThrowControlTuning();
            var v = new Vector2(0f, t.referenceSwipeSpeed * swipeScale);
            Assert.AreEqual(SwipeReject.None, SwipeThrowMapper.TryMap(v, v, style, lateral, t, out var p));
            return p;
        }

        /// <summary>
        /// 基準のスワイプで、棒がピンの並びの方へ飛び、手前の列に届くこと。
        /// 何本倒れるかは物理次第でばらつくので、ここでは届くかと向きだけを見る。
        /// </summary>
        [TestCase(ThrowStyle.Vertical, 0f)]
        [TestCase(ThrowStyle.Horizontal, 0f)]
        [TestCase(ThrowStyle.Vertical, -0.4f)]
        [TestCase(ThrowStyle.Horizontal, 0.4f)]
        public void ReferenceSwipe_ReachesPins(ThrowStyle style, float lateral)
        {
            var p = MapReference(style, lateral);
            _sim.Throw(p);
            _sim.RunToEnd();
            Assert.IsFalse(_sim.LastTimedOut);

            // 棒がピンに届いた瞬間: 棒の中心が手前の列の少し手前を通ったか、どれかのピンが動き始めたとき
            const float frontZ = 3.42f;
            var rec = _sim.LastRecord;
            Vector3? reach = null;
            for (int f = 1; f < rec.FrameCount && reach == null; f++)
            {
                rec.GetPose(f, MolkkySpec.StickBodyIndex, out var sp, out _);
                bool pinMoved = false;
                for (int n = 1; n <= MolkkySpec.PinCount && !pinMoved; n++)
                {
                    rec.GetPose(f, n, out var pp, out _);
                    pinMoved = Vector3.Distance(pp, MolkkySpec.InitialPinPosition(n)) > 0.005f;
                }
                if (sp.z >= frontZ || pinMoved) reach = sp;
            }
            Debug.Log($"[{style}] lateral={lateral}: speed={p.speed:F2} yaw={p.yawDeg:F1} reach={reach} " +
                      $"fallen=[{string.Join(",", _sim.LastFallenPins)}]");

            Assert.IsTrue(reach.HasValue, "stick did not reach the pins");
            Assert.That(reach.Value.x, Is.EqualTo(SwipeThrowMapper.PinsCenter.x).Within(0.1f), "not aimed at the pins");
            Assert.Less(reach.Value.y, MolkkySpec.PinHeight + MolkkySpec.StickLength * 0.5f, "flew over the pins");
        }

        /// <summary>
        /// まっすぐ投げたら、ピンがなくても横に大きく曲がらないこと（回転のかけ方の確認）。
        /// スワイプの速さを ±10% 変えても、止まる距離の差が小さいこと。
        /// </summary>
        [TestCase(ThrowStyle.Vertical)]
        [TestCase(ThrowStyle.Horizontal)]
        public void StraightThrow_StaysStraight_AndDistanceIsForgiving(ThrowStyle style)
        {
            // ピンをどけて棒だけにする
            for (int n = 1; n <= MolkkySpec.PinCount; n++)
            {
                var b = _rig.GetBody(n);
                b.isKinematic = true;
                b.position = new Vector3(5f + n, 0f, -5f);
                b.transform.position = b.position;
            }
            Physics.SyncTransforms();

            var ends = new List<Vector3>();
            foreach (float scale in new[] { 0.9f, 1f, 1.1f })
            {
                _sim.Throw(MapReference(style, 0f, scale));
                _sim.RunToEnd();
                ends.Add(_rig.GetBody(MolkkySpec.StickBodyIndex).position);
            }
            Debug.Log($"[{style}] straight ends: {string.Join(" ", ends)}");

            foreach (var e in ends) Assert.Less(Mathf.Abs(e.x), 0.3f, $"curved sideways: {e}");
            Assert.Less(ends[2].z - ends[0].z, 1.0f, "distance too sensitive to swipe speed");
        }
    }
}
