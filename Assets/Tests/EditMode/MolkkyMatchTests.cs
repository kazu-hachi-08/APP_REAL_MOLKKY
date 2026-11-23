using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RealMolkky.Molkky;
using UnityEngine;

namespace RealMolkky.Tests
{
    public class MolkkyMatchTests
    {
        static MolkkyMatch Individual(int count, int seed = 1)
        {
            var names = Enumerable.Range(1, count).Select(i => $"P{i}").ToList();
            return new MolkkyMatch(MatchMode.Individual, names, seed);
        }

        /// <summary>今の番の人が points 点、他の人は 1 点ずつ（試合を進めるため）</summary>
        static void ThrowsUntil(MolkkyMatch m, MatchSide target, params int[] points)
        {
            foreach (int p in points)
            {
                while (m.CurrentSide != target) m.ApplyThrow(1);
                m.ApplyThrow(p);
            }
        }

        [Test]
        public void TurnOrder_IsRandomPerSeed_AndContainsEveryone()
        {
            var orders = new HashSet<string>();
            for (int seed = 0; seed < 30; seed++)
            {
                var m = Individual(4, seed);
                var names = m.Sides.Select(s => s.Name).ToList();
                CollectionAssert.AreEquivalent(new[] { "P1", "P2", "P3", "P4" }, names);
                orders.Add(string.Join(",", names));
                // 同じ種なら同じ順番（オンラインで種を共有する前提）
                CollectionAssert.AreEqual(names, Individual(4, seed).Sides.Select(s => s.Name).ToList());
            }
            Assert.Greater(orders.Count, 5, "order should vary with seed");
        }

        [Test]
        public void Turns_RotateThroughPlayers()
        {
            var m = Individual(3);
            var first = m.CurrentSide;
            m.ApplyThrow(5);
            Assert.AreNotSame(first, m.CurrentSide);
            m.ApplyThrow(5);
            m.ApplyThrow(5);
            Assert.AreSame(first, m.CurrentSide);
            Assert.AreEqual(5, first.Score);
        }

        [Test]
        public void ExactlyFifty_Wins()
        {
            var m = Individual(2);
            var a = m.CurrentSide;
            ThrowsUntil(m, a, 12, 12, 12, 12);
            Assert.AreEqual(48, a.Score);
            Assert.IsFalse(m.IsOver);
            while (m.CurrentSide != a) m.ApplyThrow(1);
            var r = m.ApplyThrow(2);
            Assert.IsTrue(r.matchOver);
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(m.Sides.ToList().IndexOf(a), m.WinnerIndex);
            Assert.Throws<System.InvalidOperationException>(() => m.ApplyThrow(1));
        }

        [Test]
        public void OverFifty_DropsTo25()
        {
            var m = Individual(2);
            var a = m.CurrentSide;
            ThrowsUntil(m, a, 12, 12, 12, 10); // 46
            while (m.CurrentSide != a) m.ApplyThrow(1);
            var r = m.ApplyThrow(5); // 51
            Assert.IsTrue(r.overflow);
            Assert.AreEqual(46, r.scoreBefore);
            Assert.AreEqual(25, r.scoreAfter);
            Assert.AreEqual(25, a.Score);
            Assert.IsFalse(m.IsOver);
        }

        [Test]
        public void ThreeMissesInARow_Disqualifies_ScoreResetsCount()
        {
            var m = Individual(3);
            var a = m.CurrentSide;
            ThrowsUntil(m, a, 0, 0);
            Assert.AreEqual(2, a.Misses);
            ThrowsUntil(m, a, 3); // 点を取ったら数え直し
            Assert.AreEqual(0, a.Misses);
            ThrowsUntil(m, a, 0, 0);
            while (m.CurrentSide != a) m.ApplyThrow(1);
            var r = m.ApplyThrow(0);
            Assert.IsTrue(r.disqualified);
            Assert.IsTrue(a.Disqualified);
            Assert.IsFalse(m.IsOver, "two players remain");

            // 失格した人の番は飛ばす
            for (int i = 0; i < 10; i++)
            {
                Assert.AreNotSame(a, m.CurrentSide);
                m.ApplyThrow(1);
            }
        }

        [Test]
        public void LastRemainingPlayer_Wins()
        {
            var m = Individual(2);
            var a = m.CurrentSide;
            var b = m.Sides.First(s => s != a);
            for (int i = 0; i < 2; i++)
            {
                m.ApplyThrow(0); // a
                m.ApplyThrow(4); // b
            }
            var r = m.ApplyThrow(0);
            Assert.IsTrue(r.disqualified);
            Assert.IsTrue(r.matchOver);
            Assert.AreEqual(m.Sides.ToList().IndexOf(b), m.WinnerIndex);
        }

        [Test]
        public void SoloPlayer_Disqualified_NoWinner()
        {
            var m = Individual(1);
            m.ApplyThrow(0);
            m.ApplyThrow(0);
            var r = m.ApplyThrow(0);
            Assert.IsTrue(r.matchOver);
            Assert.AreEqual(-1, m.WinnerIndex);
        }

        [Test]
        public void Team_MembersAlternate_ScoreShared()
        {
            var names = new[] { "A1", "A2", "B1", "B2" };
            var m = new MolkkyMatch(MatchMode.Team, names, 7);
            Assert.AreEqual(2, m.Sides.Count);
            var teamA = m.Sides.First(s => s.Members.Contains("A1"));
            CollectionAssert.AreEquivalent(new[] { "A1", "A2" }, teamA.Members);

            var throwers = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                throwers.Add(m.CurrentThrower);
                m.ApplyThrow(2);
            }
            // 陣営が交互、各チームの中でもメンバーが交互
            for (int i = 0; i + 2 < throwers.Count; i++)
            {
                Assert.AreNotEqual(throwers[i][0], throwers[i + 1][0], "teams alternate");
                Assert.AreNotEqual(throwers[i], throwers[i + 2], "members alternate");
            }
            Assert.AreEqual(8, teamA.Score);
        }

        [Test]
        public void Team_MissesAreCountedPerTeam()
        {
            var m = new MolkkyMatch(MatchMode.Team, new[] { "A1", "A2", "B1", "B2" }, 3);
            var first = m.CurrentSide;
            m.ApplyThrow(0); // 1人目
            m.ApplyThrow(5);
            m.ApplyThrow(0); // 2人目
            m.ApplyThrow(5);
            var r = m.ApplyThrow(0); // また1人目 → チームで3回連続
            Assert.IsTrue(r.disqualified);
            Assert.IsTrue(first.Disqualified);
            Assert.IsTrue(m.IsOver);
        }

        [Test]
        public void InvalidPlayerCounts_Throw()
        {
            Assert.Throws<System.ArgumentException>(() => Individual(0));
            Assert.Throws<System.ArgumentException>(() => Individual(5));
            Assert.Throws<System.ArgumentException>(() => new MolkkyMatch(MatchMode.Team, new[] { "a", "b", "c" }, 0));
        }

        // ---- 立て直し ----

        static readonly Rect Area = Rect.MinMaxRect(-10f, -6.5f, 10f, 13.5f);

        static void InitialPoses(out Vector3[] pos, out Quaternion[] rot)
        {
            pos = new Vector3[MolkkySpec.PinCount];
            rot = new Quaternion[MolkkySpec.PinCount];
            for (int i = 0; i < pos.Length; i++)
            {
                pos[i] = MolkkySpec.InitialPinPosition(i + 1);
                rot[i] = Quaternion.identity;
            }
        }

        static void AssertNoOverlap(Vector3[] pos)
        {
            for (int a = 0; a < pos.Length; a++)
            for (int b = a + 1; b < pos.Length; b++)
            {
                var d = new Vector2(pos[a].x - pos[b].x, pos[a].z - pos[b].z).magnitude;
                Assert.GreaterOrEqual(d, MolkkySpec.PinDiameter, $"pin {a + 1} and {b + 1} overlap ({d * 1000f:F1}mm)");
            }
        }

        [Test]
        public void Restand_StandingPinsUnchanged_FallenPinStoodAtItsCenter()
        {
            InitialPoses(out var pos, out var rot);
            // 7番を奥に倒す（手前に寝かせる向き）
            var fallenRot = Quaternion.Euler(90f, 20f, 0f);
            var fallenPos = new Vector3(0.4f, MolkkySpec.PinRadius, 4.3f);
            pos[6] = fallenPos;
            rot[6] = fallenRot;
            // 3番は少しだけ傾いて立っている
            rot[2] = Quaternion.Euler(3f, 40f, 0f);

            var outPos = new Vector3[12];
            var outRot = new Quaternion[12];
            var restood = new bool[12];
            PinRestand.Compute(pos, rot, Area, 0f, outPos, outRot, restood);

            var center = fallenPos + fallenRot * Vector3.up * (MolkkySpec.PinHeight * 0.5f);
            Assert.AreEqual(center.x, outPos[6].x, 1e-4f);
            Assert.AreEqual(center.z, outPos[6].z, 1e-4f);
            Assert.AreEqual(0f, outPos[6].y);
            Assert.Less(Quaternion.Angle(outRot[6], Quaternion.identity), 0.01f);
            Assert.IsTrue(restood[6]);

            for (int i = 0; i < 12; i++)
            {
                Assert.Less(MolkkySpec.PinTiltDeg(outRot[i]), 0.01f, $"pin {i + 1} upright");
                if (i == 6) continue;
                Assert.IsFalse(restood[i]);
                Assert.Less(Vector3.Distance(outPos[i], MolkkySpec.InitialPinPosition(i + 1)), 1e-5f);
            }
            // 傾いて立っていたピンは向き（鉛直軸回り）を保つ
            Assert.AreEqual(40f, outRot[2].eulerAngles.y, 0.5f);
        }

        [Test]
        public void Restand_FallenOntoStandingPin_IsMovedAside()
        {
            InitialPoses(out var pos, out var rot);
            // 12番を、中心がちょうど5番の位置に来るように倒す
            var fallenRot = Quaternion.Euler(0f, 0f, 90f);
            var target = MolkkySpec.InitialPinPosition(5);
            pos[11] = target - fallenRot * Vector3.up * (MolkkySpec.PinHeight * 0.5f) + Vector3.up * MolkkySpec.PinRadius;
            rot[11] = fallenRot;

            var outPos = new Vector3[12];
            var outRot = new Quaternion[12];
            PinRestand.Compute(pos, rot, Area, 0f, outPos, outRot);

            AssertNoOverlap(outPos);
            Assert.Less(Vector3.Distance(outPos[4], target), 1e-5f, "standing pin 5 stays");
            Assert.Less(Vector2.Distance(new Vector2(outPos[11].x, outPos[11].z), new Vector2(target.x, target.z)), 0.15f, "moved only a little");
        }

        [Test]
        public void Restand_ManyFallenInAPile_NoOverlap_Deterministic()
        {
            var pos = new Vector3[12];
            var rot = new Quaternion[12];
            for (int i = 0; i < 12; i++)
            {
                // 全部ほぼ同じ場所に倒れている（2本は完全に同じ）
                rot[i] = Quaternion.Euler(90f, i * 30f, 0f);
                var c = new Vector3(0.01f * (i % 5), MolkkySpec.PinRadius, 4f);
                if (i == 1) c = new Vector3(0f, MolkkySpec.PinRadius, 4f);
                pos[i] = c - rot[i] * Vector3.up * (MolkkySpec.PinHeight * 0.5f) + Vector3.up * MolkkySpec.PinRadius;
            }
            var a = new Vector3[12];
            var b = new Vector3[12];
            var r = new Quaternion[12];
            PinRestand.Compute(pos, rot, Area, 0f, a, r);
            PinRestand.Compute(pos, rot, Area, 0f, b, r);
            AssertNoOverlap(a);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Restand_OutsideGround_ClampedInside()
        {
            InitialPoses(out var pos, out var rot);
            pos[0] = new Vector3(15f, -3f, 4f); // 地面の外に落ちた
            rot[0] = Quaternion.Euler(90f, 0f, 0f);
            var outPos = new Vector3[12];
            var outRot = new Quaternion[12];
            PinRestand.Compute(pos, rot, Area, 0f, outPos, outRot);
            Assert.LessOrEqual(outPos[0].x, Area.xMax - MolkkySpec.PinRadius + 1e-4f);
            Assert.AreEqual(0f, outPos[0].y);
        }
    }
}
