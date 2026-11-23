using System.Collections;
using System.Linq;
using NUnit.Framework;
using RealMolkky.Molkky;
using RealMolkky.Play;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RealMolkky.Tests
{
    /// <summary>
    /// フェーズ3のシーンを実際に動かして、投げ → 得点 → 立て直し → 次の人 の流れを確かめる。
    /// </summary>
    public class MatchFlowTests
    {
        const string SceneName = "Phase3Match";

        MatchController _match;
        ThrowController _controller;
        MolkkyRig _rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            _match = Object.FindAnyObjectByType<MatchController>();
            _controller = Object.FindAnyObjectByType<ThrowController>();
            _rig = Object.FindAnyObjectByType<MolkkyRig>();
            Assert.IsNotNull(_match);
            Assert.AreEqual(MatchController.Phase.Setup, _match.CurrentPhase);
            Assert.IsFalse(_controller.InputEnabled, "no throwing on the setup screen");
        }

        IEnumerator WaitFor(System.Func<bool> cond, float timeout, string what)
        {
            float t = 0f;
            while (!cond())
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout) Assert.Fail($"timeout waiting for {what}");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Throw_Scores_RestandsPins_NextPlayer()
        {
            _match.StartMatch(MatchMode.Individual, new[] { "Aki", "Ben" });
            yield return null;
            Assert.AreEqual(MatchController.Phase.Playing, _match.CurrentPhase);
            var first = _match.Match.CurrentSide;

            Assert.IsTrue(_controller.Throw(ThrowParams.Default));
            yield return WaitFor(() => _match.CurrentPhase == MatchController.Phase.ShowingResult, 30f, "result");

            var r = _match.LastResult;
            Debug.Log($"[MatchFlowTests] fallen=[{string.Join(",", _match.LastFallenPins)}] points={r.points}");
            Assert.Greater(_match.LastFallenPins.Count, 0, "default throw knocks pins down");
            Assert.AreEqual(MolkkyScoring.ThrowScore(_match.LastFallenPins.ToList()), r.points);
            Assert.AreEqual(r.points, first.Score);
            Assert.IsFalse(_controller.InputEnabled, "no throwing while showing the result");

            _match.SkipResult();
            yield return WaitFor(() => _match.CurrentPhase == MatchController.Phase.Playing, 5f, "next turn");

            Assert.AreNotSame(first, _match.Match.CurrentSide, "turn passes to the other player");
            Assert.IsTrue(_controller.InputEnabled);
            Assert.AreEqual(ThrowController.State.Aiming, _controller.CurrentState);
            for (int n = 1; n <= MolkkySpec.PinCount; n++)
            {
                var tr = _rig.GetBody(n).transform;
                Assert.Less(MolkkySpec.PinTiltDeg(tr.rotation), 2f, $"pin {n} stood up");
            }

            // 立て直したピンが動かないことを少し待って確かめる
            var before = Enumerable.Range(1, MolkkySpec.PinCount).Select(n => _rig.GetBody(n).transform.position).ToArray();
            yield return new WaitForSeconds(0.5f);
            for (int n = 1; n <= MolkkySpec.PinCount; n++)
            {
                Assert.Less(Vector3.Distance(before[n - 1], _rig.GetBody(n).transform.position), 0.002f, $"pin {n} still");
            }
        }

        [UnityTest]
        public IEnumerator ThreeMisses_SoloGameOver_ThenRematchAndBackToSetup()
        {
            _match.StartMatch(MatchMode.Individual, new[] { "Solo" });
            var miss = ThrowParams.Default;
            miss.yawDeg = 30f; // 大きく外す

            for (int i = 0; i < MolkkyMatch.MaxConsecutiveMisses; i++)
            {
                yield return WaitFor(() => _match.CurrentPhase == MatchController.Phase.Playing, 5f, "turn");
                Assert.IsTrue(_controller.Throw(miss));
                yield return WaitFor(() => _match.CurrentPhase == MatchController.Phase.ShowingResult, 30f, "result");
                Assert.IsTrue(_match.LastResult.missed, $"throw {i + 1} should miss");
                _match.SkipResult();
            }
            yield return WaitFor(() => _match.CurrentPhase == MatchController.Phase.GameOver, 5f, "game over");
            Assert.IsTrue(_match.Match.Sides[0].Disqualified);
            Assert.AreEqual(-1, _match.Match.WinnerIndex);

            _match.Rematch();
            Assert.AreEqual(MatchController.Phase.Playing, _match.CurrentPhase);
            Assert.AreEqual(0, _match.Match.ThrowCount);
            _match.BackToSetup();
            Assert.AreEqual(MatchController.Phase.Setup, _match.CurrentPhase);
            Assert.IsFalse(_controller.InputEnabled);
        }
    }
}
