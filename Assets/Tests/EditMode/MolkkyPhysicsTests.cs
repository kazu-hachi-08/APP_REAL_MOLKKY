using System.Collections.Generic;
using NUnit.Framework;
using RealMolkky.Molkky;
using UnityEngine;

namespace RealMolkky.Tests
{
    /// <summary>
    /// 実際に物理を回して確かめる。エディタ上で Physics.Simulate を手動で呼ぶ。
    /// </summary>
    public class MolkkyPhysicsTests
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

        [Test]
        public void Masses_AreRealistic()
        {
            float stick = _rig.GetBody(0).mass;
            float pin = _rig.GetBody(1).mass;
            Debug.Log($"stick {stick:F3}kg pin {pin:F3}kg");
            Assert.That(stick, Is.InRange(0.3f, 0.4f));
            Assert.That(pin, Is.InRange(0.17f, 0.23f));
        }

        [Test]
        public void StandingPins_StayStill()
        {
            // 眠らせずに揺れを見るため、全部起こしてから3秒進める
            for (int i = 1; i <= MolkkySpec.PinCount; i++) _rig.GetBody(i).WakeUp();
            float dt = _rig.tuning.fixedDeltaTime;
            for (int s = 0; s < Mathf.RoundToInt(3f / dt); s++)
            {
                _rig.PreStep(dt);
                Physics.Simulate(dt);
            }

            for (int n = 1; n <= MolkkySpec.PinCount; n++)
            {
                var tr = _rig.GetBody(n).transform;
                float moved = Vector3.Distance(tr.position, MolkkySpec.InitialPinPosition(n));
                Assert.Less(moved, 0.002f, $"pin {n} drifted {moved * 1000f:F2}mm");
                Assert.Less(MolkkySpec.PinTiltDeg(tr.rotation), 1f, $"pin {n} tilted");
            }
        }

        [TestCase(ThrowStyle.Vertical)]
        [TestCase(ThrowStyle.Horizontal)]
        public void RestoodPins_StayStanding(ThrowStyle style)
        {
            var p = ThrowParams.Default;
            p.style = style;
            _sim.Throw(p);
            _sim.RunToEnd();
            Assume.That(_sim.LastFallenPins.Count, Is.GreaterThan(0));

            _rig.GroundArea(out var area, out float groundY);
            var pos = new Vector3[MolkkySpec.PinCount];
            var rot = new Quaternion[MolkkySpec.PinCount];
            PinRestand.FromRecord(_sim.LastRecord, area, groundY, pos, rot);
            _rig.PlacePins(pos, rot);

            // 眠らせずに2秒進めても、倒れず・ずれない
            for (int i = 1; i <= MolkkySpec.PinCount; i++) _rig.GetBody(i).WakeUp();
            float dt = _rig.tuning.fixedDeltaTime;
            for (int s = 0; s < Mathf.RoundToInt(2f / dt); s++)
            {
                _rig.PreStep(dt);
                Physics.Simulate(dt);
            }
            for (int n = 1; n <= MolkkySpec.PinCount; n++)
            {
                var tr = _rig.GetBody(n).transform;
                Assert.Less(MolkkySpec.PinTiltDeg(tr.rotation), 2f, $"pin {n} tilted");
                Assert.Less(Vector3.Distance(tr.position, pos[n - 1]), 0.005f, $"pin {n} moved");
            }
        }

        [TestCase(ThrowStyle.Vertical)]
        [TestCase(ThrowStyle.Horizontal)]
        public void DefaultThrow_StopsAndHitsPins(ThrowStyle style)
        {
            var p = ThrowParams.Default;
            p.style = style;
            _sim.Throw(p);
            _sim.RunToEnd();

            Debug.Log($"{style}: t={_sim.SimTime:F2}s fallen=[{string.Join(",", _sim.LastFallenPins)}] " +
                      $"score={_sim.LastScore} frames={_sim.LastRecord.FrameCount} bytes={_sim.LastRecord.ToBytes().Length}");

            Assert.AreEqual(ThrowSimulator.State.Finished, _sim.CurrentState);
            Assert.IsFalse(_sim.LastTimedOut, "did not settle before timeout");
            Assert.Greater(_sim.LastFallenPins.Count, 0, "no pin knocked down");

            // 記録の最終フレーム = 物理の最終位置
            var rec = _sim.LastRecord;
            for (int b = 0; b < MolkkySpec.BodyCount; b++)
            {
                rec.GetPose(rec.FrameCount - 1, b, out var pos, out var rot);
                var tr = _rig.GetBody(b).transform;
                Assert.Less(Vector3.Distance(pos, tr.position), 1e-5f);
                Assert.Less(Quaternion.Angle(rot, tr.rotation), 0.01f);
            }

            // バイト列から戻しても同じ判定になる（オンラインで各端末が計算する得点）
            var copy = ThrowRecord.FromBytes(rec.ToBytes());
            CollectionAssert.AreEqual(_sim.LastFallenPins, MolkkyScoring.FallenPins(copy));
        }
    }
}
