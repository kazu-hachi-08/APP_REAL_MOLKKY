using System.Collections.Generic;
using NUnit.Framework;
using RealMolkky.Molkky;
using UnityEngine;

namespace RealMolkky.Tests
{
    public class MolkkyRulesTests
    {
        [Test]
        public void PinLayout_FrontRowAtThrowDistance_NoOverlap()
        {
            Assert.AreEqual(MolkkySpec.ThrowDistance, MolkkySpec.InitialPinPosition(1).z, 1e-5f);
            Assert.AreEqual(MolkkySpec.ThrowDistance, MolkkySpec.InitialPinPosition(2).z, 1e-5f);
            for (int a = 1; a <= 12; a++)
            for (int b = a + 1; b <= 12; b++)
            {
                float d = Vector3.Distance(MolkkySpec.InitialPinPosition(a), MolkkySpec.InitialPinPosition(b));
                Assert.GreaterOrEqual(d, MolkkySpec.PinDiameter, $"pin {a} and {b} overlap");
            }
            // 12番は3列目の中央寄り、奥の列は 7-9-8
            Assert.Less(MolkkySpec.InitialPinPosition(7).x, MolkkySpec.InitialPinPosition(9).x);
            Assert.Less(MolkkySpec.InitialPinPosition(9).x, MolkkySpec.InitialPinPosition(8).x);
        }

        [Test]
        public void IsPinFallen_Upright_False()
        {
            Assert.IsFalse(MolkkySpec.IsPinFallen(Vector3.zero, Quaternion.identity));
        }

        [Test]
        public void IsPinFallen_LyingOnGround_True()
        {
            // 横倒し: 底面の中心は半径の高さ
            var rot = Quaternion.Euler(90f, 30f, 0f);
            Assert.IsTrue(MolkkySpec.IsPinFallen(new Vector3(0.1f, MolkkySpec.PinRadius, 3.6f), rot));
        }

        [Test]
        public void IsPinFallen_LeaningOnStick_False()
        {
            // 地面に寝た棒（直径5.5cm）に寄りかかる: 水平から約20度
            float fromHorizontal = 20f;
            var rot = Quaternion.Euler(90f - fromHorizontal, 0f, 0f);
            // 底面の縁が地面に付いている
            var pos = new Vector3(0f, MolkkySpec.PinRadius * Mathf.Cos(fromHorizontal * Mathf.Deg2Rad), 0f);
            Assert.IsFalse(MolkkySpec.IsPinFallen(pos, rot));
        }

        [Test]
        public void IsPinFallen_LeaningOnPin_False()
        {
            Assert.IsFalse(MolkkySpec.IsPinFallen(Vector3.zero, Quaternion.Euler(45f, 0f, 0f)));
        }

        [Test]
        public void IsPinFallen_LyingOnTopOfOtherPin_False()
        {
            var rot = Quaternion.Euler(90f, 0f, 0f);
            var pos = new Vector3(0f, MolkkySpec.PinDiameter + MolkkySpec.PinRadius, 0f);
            Assert.IsFalse(MolkkySpec.IsPinFallen(pos, rot));
        }

        [Test]
        public void ThrowScore_Rules()
        {
            Assert.AreEqual(0, MolkkyScoring.ThrowScore(new List<int>()));
            Assert.AreEqual(7, MolkkyScoring.ThrowScore(new List<int> { 7 }));
            Assert.AreEqual(3, MolkkyScoring.ThrowScore(new List<int> { 12, 1, 5 }));
        }

        [Test]
        public void ThrowRecord_BytesRoundTrip_AndSample()
        {
            var rec = new ThrowRecord(0.05f, MolkkySpec.BodyCount, 1);
            for (int f = 0; f < 5; f++)
            {
                int frame = rec.AddFrame();
                for (int b = 0; b < MolkkySpec.BodyCount; b++)
                {
                    rec.SetPose(frame, b, new Vector3(b, f, -f), Quaternion.Euler(0f, f * 10f, 0f));
                }
            }

            var copy = ThrowRecord.FromBytes(rec.ToBytes());
            Assert.AreEqual(5, copy.FrameCount);
            Assert.AreEqual(0.05f, copy.FrameInterval);
            Assert.AreEqual(0.2f, copy.Duration, 1e-5f);

            copy.GetPose(3, 4, out var p, out var r);
            Assert.AreEqual(new Vector3(4, 3, -3), p);
            Assert.Less(Quaternion.Angle(r, Quaternion.Euler(0f, 30f, 0f)), 0.01f);

            copy.Sample(0.125f, 2, out p, out r); // フレーム2と3の中間
            Assert.AreEqual(2.5f, p.y, 1e-4f);
            Assert.Less(Quaternion.Angle(r, Quaternion.Euler(0f, 25f, 0f)), 0.01f);

            copy.Sample(99f, 0, out p, out _); // 最後で止まる
            Assert.AreEqual(4f, p.y, 1e-4f);
        }
    }
}
