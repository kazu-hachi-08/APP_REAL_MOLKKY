using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RealMolkky.Molkky
{
    public enum ThrowStyle
    {
        /// <summary>縦投げ: 棒を立てて縦に回す。1本を狙う</summary>
        Vertical,
        /// <summary>横投げ: 棒を寝かせて投げる。まとめて倒す</summary>
        Horizontal,
    }

    /// <summary>投げる瞬間の条件。フェーズ2でスワイプから作る。</summary>
    [Serializable]
    public struct ThrowParams
    {
        public ThrowStyle style;
        [Tooltip("初速 m/s")] public float speed;
        [Tooltip("仰角 度")] public float elevationDeg;
        [Tooltip("左右の向き 度（+で右）")] public float yawDeg;
        [Tooltip("回転 rad/s（縦投げは前回り、横投げは水平回り）")] public float spin;
        [Tooltip("投げる位置（投げるラインからの左右）")] public float lateral;
        [Tooltip("手を離す高さ m")] public float releaseHeight;
        [Tooltip("投げるラインから手を離す位置までの前方距離 m")] public float releaseForward;

        public static ThrowParams Default => new ThrowParams
        {
            style = ThrowStyle.Vertical,
            speed = 6.0f,
            elevationDeg = 18f,
            yawDeg = 0f,
            spin = 6f,
            lateral = 0f,
            releaseHeight = 0.6f,
            releaseForward = 0.3f,
        };
    }

    /// <summary>
    /// 物理を自前のループで進め、投げの記録を作る。止まったら結果を返す。
    /// Physics.simulationMode = Script にして、1ステップの処理時間も測る（スマホでの重さの確認用）。
    /// </summary>
    public class ThrowSimulator : MonoBehaviour
    {
        public enum State { Idle, Simulating, Finished }

        [SerializeField] MolkkyRig rig;

        public State CurrentState { get; private set; } = State.Idle;
        public ThrowRecord LastRecord { get; private set; }
        public List<int> LastFallenPins { get; private set; } = new List<int>();
        public int LastScore { get; private set; }
        public bool LastTimedOut { get; private set; }

        public float SimTime { get; private set; }
        public int StepCount { get; private set; }

        // 処理時間（ms）
        public float AvgStepMs { get; private set; }
        public float MaxStepMs { get; private set; }
        public int DroppedSteps { get; private set; }

        public event Action<ThrowRecord> Finished;

        float _accum;
        float _slowTimer;
        double _stepMsTotal;
        readonly Stopwatch _sw = new Stopwatch();

        public void Bind(MolkkyRig r) => rig = r;

        void Awake()
        {
            Physics.simulationMode = SimulationMode.Script;
        }

        public void ResetPins()
        {
            CurrentState = State.Idle;
            rig.ApplyTuning();
            rig.ResetPins();
        }

        public static void ReleasePose(in ThrowParams p, out Vector3 position, out Quaternion rotation,
            out Vector3 velocity, out Vector3 angularVelocity)
        {
            var yaw = Quaternion.Euler(0f, p.yawDeg, 0f);
            position = new Vector3(p.lateral, p.releaseHeight, p.releaseForward);

            // 棒の軸はローカル +Y。縦投げは立てたまま、横投げは左右に寝かせる
            rotation = p.style == ThrowStyle.Vertical
                ? yaw * Quaternion.Euler(-15f, 0f, 0f) // 少し手前に傾けて持つ
                : yaw * Quaternion.Euler(0f, 0f, 90f);

            float el = p.elevationDeg * Mathf.Deg2Rad;
            velocity = yaw * new Vector3(0f, Mathf.Sin(el), Mathf.Cos(el)) * p.speed;

            // 縦投げ: 上端が前に倒れる向き（+X軸回り）。横投げ: 鉛直軸回り
            angularVelocity = p.style == ThrowStyle.Vertical
                ? yaw * Vector3.right * p.spin
                : Vector3.up * p.spin;
        }

        public void Throw(in ThrowParams p)
        {
            if (CurrentState == State.Simulating) return;

            rig.ApplyTuning();
            ReleasePose(p, out var pos, out var rot, out var vel, out var angVel);
            rig.ReleaseStick(pos, rot, vel, angVel);

            var t = rig.tuning;
            LastRecord = new ThrowRecord(t.fixedDeltaTime * Mathf.Max(1, t.recordEverySteps));
            rig.WritePoses(LastRecord, LastRecord.AddFrame());

            SimTime = 0f;
            StepCount = 0;
            _accum = 0f;
            _slowTimer = 0f;
            _stepMsTotal = 0;
            AvgStepMs = 0f;
            MaxStepMs = 0f;
            DroppedSteps = 0;
            LastTimedOut = false;
            CurrentState = State.Simulating;
        }

        void Update()
        {
            if (CurrentState != State.Simulating) return;

            var t = rig.tuning;
            float dt = t.fixedDeltaTime;
            _accum += Time.deltaTime;

            int steps = 0;
            while (_accum >= dt && CurrentState == State.Simulating)
            {
                if (steps >= t.maxStepsPerFrame)
                {
                    // 追いつけない分は捨てる（見た目がスローになるだけで結果には影響しない）
                    DroppedSteps += Mathf.FloorToInt(_accum / dt);
                    _accum = 0f;
                    break;
                }
                Step(dt);
                _accum -= dt;
                steps++;
            }
        }

        void Step(float dt)
        {
            var t = rig.tuning;
            rig.PreStep(dt);

            _sw.Restart();
            Physics.Simulate(dt);
            _sw.Stop();

            float ms = (float)_sw.Elapsed.TotalMilliseconds;
            _stepMsTotal += ms;
            StepCount++;
            SimTime += dt;
            AvgStepMs = (float)(_stepMsTotal / StepCount);
            MaxStepMs = Mathf.Max(MaxStepMs, ms);

            if (StepCount % Mathf.Max(1, t.recordEverySteps) == 0)
            {
                rig.WritePoses(LastRecord, LastRecord.AddFrame());
            }

            // 止まった判定: 全部が遅い状態が一定時間続く
            rig.MaxSpeeds(out float lin, out float ang);
            bool slow = lin < t.stopLinearSpeed && ang < t.stopAngularSpeed;
            _slowTimer = slow ? _slowTimer + dt : 0f;

            bool stopped = SimTime > 0.2f && _slowTimer >= t.stopHoldTime;
            bool timeout = SimTime >= t.simulationTimeout;
            if (stopped || timeout) Finish(timeout);
        }

        void Finish(bool timedOut)
        {
            // 最終位置を必ず最後のフレームとして残す
            if (StepCount % Mathf.Max(1, rig.tuning.recordEverySteps) != 0)
            {
                rig.WritePoses(LastRecord, LastRecord.AddFrame());
            }

            LastTimedOut = timedOut;
            LastFallenPins = MolkkyScoring.FallenPins(LastRecord);
            LastScore = MolkkyScoring.ThrowScore(LastFallenPins);
            CurrentState = State.Finished;

            Debug.Log($"[ThrowSimulator] {(timedOut ? "Timeout" : "Stopped")} t={SimTime:F2}s steps={StepCount} " +
                      $"avg={AvgStepMs:F2}ms max={MaxStepMs:F2}ms fallen=[{string.Join(",", LastFallenPins)}] score={LastScore}");
            Finished?.Invoke(LastRecord);
        }

        /// <summary>テストやエディタ用: 止まるまで一気に計算する</summary>
        public void RunToEnd()
        {
            while (CurrentState == State.Simulating) Step(rig.tuning.fixedDeltaTime);
        }
    }
}
