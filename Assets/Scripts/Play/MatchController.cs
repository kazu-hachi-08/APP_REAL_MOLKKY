using System;
using System.Collections.Generic;
using RealMolkky.Molkky;
using UnityEngine;

namespace RealMolkky.Play
{
    /// <summary>
    /// 1台を回して遊ぶ試合の流れ: 投げる → 止まる → 得点 → 結果を見せる → 立て直し → 次の人。
    /// ルールは <see cref="MolkkyMatch"/>、立て直しの位置は <see cref="PinRestand"/> が決める。
    /// </summary>
    public class MatchController : MonoBehaviour
    {
        public enum Phase
        {
            /// <summary>名前入力と個人戦・チーム戦の選択</summary>
            Setup,
            /// <summary>投げる番（投げて止まるまで）</summary>
            Playing,
            /// <summary>投げの結果を見せている</summary>
            ShowingResult,
            /// <summary>試合が終わった</summary>
            GameOver,
        }

        [SerializeField] MolkkyRig rig;
        [SerializeField] ThrowSimulator simulator;
        [SerializeField] ThrowController controller;

        [Tooltip("投げの結果を見せる秒数（画面を押すと先に進む）")]
        public float resultShowTime = 2.5f;
        [Tooltip("立て直したピンを見せてから投げられるようになるまでの秒数")]
        public float restandPause = 0.3f;

        public Phase CurrentPhase { get; private set; } = Phase.Setup;
        public MolkkyMatch Match { get; private set; }
        public TurnResult LastResult { get; private set; }
        public IReadOnlyList<int> LastFallenPins => _lastFallen;
        public float ResultTimer { get; private set; }

        /// <summary>前回の試合の設定（もう一度遊ぶ用）</summary>
        public MatchMode LastMode { get; private set; }
        public IReadOnlyList<string> LastNames => _lastNames;

        public event Action<TurnResult> TurnFinished;

        readonly List<int> _lastFallen = new List<int>();
        readonly List<string> _lastNames = new List<string>();
        readonly Vector3[] _restandPos = new Vector3[MolkkySpec.PinCount];
        readonly Quaternion[] _restandRot = new Quaternion[MolkkySpec.PinCount];
        float _pauseTimer = -1f;

        public void Bind(MolkkyRig r, ThrowSimulator s, ThrowController c)
        {
            rig = r;
            simulator = s;
            controller = c;
        }

        void Awake()
        {
            controller.autoAdvance = false;
            controller.allowResetKey = false;
            controller.InputEnabled = false;
            controller.Settled += OnSettled;
        }

        void OnDestroy()
        {
            if (controller != null) controller.Settled -= OnSettled;
        }

        public void StartMatch(MatchMode mode, IReadOnlyList<string> names)
        {
            var copy = new List<string>(names); // もう一度遊ぶときは names が _lastNames 自身なので先に写す
            _lastNames.Clear();
            _lastNames.AddRange(copy);
            LastMode = mode;
            int seed = Environment.TickCount ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            Match = new MolkkyMatch(mode, _lastNames, seed);
            _lastFallen.Clear();
            LastResult = default;

            controller.ResetPins();
            controller.SetStyle(ThrowStyle.Vertical);
            controller.SetLateral(0f);
            controller.InputEnabled = true;
            _pauseTimer = -1f;
            CurrentPhase = Phase.Playing;
        }

        /// <summary>同じメンバーでもう一度（順番は引き直す）</summary>
        public void Rematch() => StartMatch(LastMode, _lastNames);

        public void BackToSetup()
        {
            controller.InputEnabled = false;
            controller.ResetPins();
            Match = null;
            CurrentPhase = Phase.Setup;
        }

        void OnSettled(ThrowSimulator sim)
        {
            if (CurrentPhase != Phase.Playing || Match == null) return;
            controller.InputEnabled = false;

            _lastFallen.Clear();
            _lastFallen.AddRange(sim.LastFallenPins);
            LastResult = Match.ApplyThrow(sim.LastScore);
            ResultTimer = 0f;
            CurrentPhase = Phase.ShowingResult;
            TurnFinished?.Invoke(LastResult);
        }

        /// <summary>結果の表示を飛ばして先に進む（画面を押したとき）</summary>
        public void SkipResult()
        {
            if (CurrentPhase == Phase.ShowingResult && _pauseTimer < 0f) ResultTimer = resultShowTime;
        }

        void Update()
        {
            if (CurrentPhase != Phase.ShowingResult) return;

            if (_pauseTimer >= 0f)
            {
                // 立て直した後、少し見せてから次の人へ
                _pauseTimer += Time.deltaTime;
                if (_pauseTimer >= restandPause)
                {
                    _pauseTimer = -1f;
                    controller.SetStyle(ThrowStyle.Vertical);
                    controller.SetLateral(0f);
                    controller.BeginNextThrow();
                    controller.InputEnabled = true;
                    CurrentPhase = Phase.Playing;
                }
                return;
            }

            ResultTimer += Time.deltaTime;
            if (ResultTimer < resultShowTime) return;

            if (Match.IsOver)
            {
                CurrentPhase = Phase.GameOver;
                return;
            }
            RestandPins();
            _pauseTimer = 0f;
        }

        void RestandPins()
        {
            rig.GroundArea(out var area, out float groundY);
            PinRestand.FromRecord(simulator.LastRecord, area, groundY, _restandPos, _restandRot);
            rig.PlacePins(_restandPos, _restandRot);
        }
    }
}
