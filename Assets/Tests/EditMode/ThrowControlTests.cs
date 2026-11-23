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
        public void SingleSample_IsZero()
        {
            var tr = new SwipeTracker();
            tr.Begin(0.0, Vector2.one);
            tr.End(0.0, Vector2.one);
            Assert.AreEqual(Vector2.zero, tr.VelocityAtEnd(0.1f));
        }
    }

    public class SwipeThrowMapperTests
    {
        readonly ThrowControlTuning _t = new ThrowControlTuning();

        [Test]
        public void StraightSwipe_ThrowsStraightWithScaledSpeed()
        {
            var r = SwipeThrowMapper.TryMap(new Vector2(0f, 2f), ThrowStyle.Vertical, 0f, _t, out var p);
            Assert.AreEqual(SwipeReject.None, r);
            Assert.That(p.speed, Is.EqualTo(2f * _t.speedPerSwipe).Within(1e-4f));
            Assert.That(p.yawDeg, Is.EqualTo(0f).Within(1e-4f));
            Assert.AreEqual(ThrowStyle.Vertical, p.style);
            Assert.That(p.elevationDeg, Is.EqualTo(_t.verticalElevationDeg));
        }

        [Test]
        public void Speed_IsCapped()
        {
            SwipeThrowMapper.TryMap(new Vector2(0f, 100f), ThrowStyle.Horizontal, 0f, _t, out var p);
            Assert.That(p.speed, Is.EqualTo(_t.maxThrowSpeed));
            Assert.That(p.elevationDeg, Is.EqualTo(_t.horizontalElevationDeg));
        }

        [Test]
        public void SwipeToTheRight_AimsRight_AndIsCapped()
        {
            SwipeThrowMapper.TryMap(new Vector2(1f, 2f), ThrowStyle.Vertical, 0f, _t, out var p);
            Assert.Greater(p.yawDeg, 0f);
            float angle = Mathf.Atan2(1f, 2f) * Mathf.Rad2Deg;
            Assert.That(p.yawDeg, Is.EqualTo(Mathf.Min(angle * _t.yawPerSwipeDeg, _t.maxYawDeg)).Within(1e-3f));

            var t = _t.Clone();
            t.yawPerSwipeDeg = 1f;
            t.maxYawDeg = 5f;
            SwipeThrowMapper.TryMap(new Vector2(-1f, 1f), ThrowStyle.Vertical, 0f, t, out p);
            Assert.That(p.yawDeg, Is.EqualTo(-5f).Within(1e-4f));
        }

        [Test]
        public void BadSwipes_AreRejected()
        {
            Assert.AreEqual(SwipeReject.TooSlow, SwipeThrowMapper.TryMap(new Vector2(0f, 0.1f), ThrowStyle.Vertical, 0f, _t, out _));
            Assert.AreEqual(SwipeReject.NotForward, SwipeThrowMapper.TryMap(new Vector2(0f, -2f), ThrowStyle.Vertical, 0f, _t, out _));
            Assert.AreEqual(SwipeReject.TooSideways, SwipeThrowMapper.TryMap(new Vector2(2f, 0.3f), ThrowStyle.Vertical, 0f, _t, out _));
        }

        [Test]
        public void Stance_FacesPins()
        {
            // 左に立つと、まっすぐのスワイプはピンの方（右）へ向く
            SwipeThrowMapper.TryMap(new Vector2(0f, 2f), ThrowStyle.Vertical, -0.5f, _t, out var p);
            Assert.That(p.lateral, Is.EqualTo(-0.5f));
            Assert.Greater(p.yawDeg, 0f);

            ThrowSimulator.ReleasePose(p, out var pos, out _, out var vel, out _);
            // 速度の延長線がピンの中心の近くを通る
            float tHit = (SwipeThrowMapper.PinsCenter.z - pos.z) / vel.z;
            Assert.That(pos.x + vel.x * tHit, Is.EqualTo(SwipeThrowMapper.PinsCenter.x).Within(0.01f));

            var t = _t.Clone();
            t.faceTarget = false;
            SwipeThrowMapper.TryMap(new Vector2(0f, 2f), ThrowStyle.Vertical, -0.5f, t, out p);
            Assert.That(p.yawDeg, Is.EqualTo(0f).Within(1e-4f));
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

        /// <summary>
        /// 標準的なスワイプで、棒がピンの並びの方へ飛び、手前の列に届くこと。
        /// 何本倒れるかは物理次第でばらつくので、ここでは届くかと向きだけを見る。
        /// </summary>
        [TestCase(ThrowStyle.Vertical, 0f)]
        [TestCase(ThrowStyle.Horizontal, 0f)]
        [TestCase(ThrowStyle.Vertical, -0.4f)]
        [TestCase(ThrowStyle.Horizontal, 0.4f)]
        public void TypicalSwipe_ReachesPins(ThrowStyle style, float lateral)
        {
            var t = new ThrowControlTuning();
            // 短辺の2.4倍/秒 ≒ スマホで軽くはじく速さ
            Assert.AreEqual(SwipeReject.None, SwipeThrowMapper.TryMap(new Vector2(0f, 2.4f), style, lateral, t, out var p));
            _sim.Throw(p);
            _sim.RunToEnd();
            Assert.IsFalse(_sim.LastTimedOut);

            // 手前の列の少し手前を棒の中心が通ったときの位置
            const float frontZ = 3.42f;
            var rec = _sim.LastRecord;
            Vector3? cross = null;
            for (int f = 1; f < rec.FrameCount && cross == null; f++)
            {
                rec.GetPose(f, MolkkySpec.StickBodyIndex, out var sp, out _);
                if (sp.z >= frontZ) cross = sp;
            }
            Debug.Log($"[{style}] lateral={lateral}: speed={p.speed:F2} yaw={p.yawDeg:F1} cross={cross} " +
                      $"fallen=[{string.Join(",", _sim.LastFallenPins)}]");

            Assert.IsTrue(cross.HasValue, "stick did not reach the pins");
            Assert.That(cross.Value.x, Is.EqualTo(SwipeThrowMapper.PinsCenter.x).Within(0.08f), "not aimed at the pins");
            Assert.Less(cross.Value.y, MolkkySpec.PinHeight + MolkkySpec.StickLength * 0.5f, "flew over the pins");
        }
    }
}
