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
        [Tooltip("回転 rad/s（投げる向きに直交する水平軸回り、+で前回り）")] public float spin;
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
        float _stableTimer;
        readonly Vector3[] _snapPos = new Vector3[MolkkySpec.BodyCount];
        readonly Quaternion[] _snapRot = new Quaternion[MolkkySpec.BodyCount];
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

            // どちらも投げる向きに直交する水平軸回り（+で前回り）。
            // 縦投げは縦に回り、横投げは寝かせた棒が長い軸のまわりに転がるように回る。
            // 鉛直軸回り（ヘリコプター状）にすると、着地で回転が横向きの動きに変わり大きく曲がる
            angularVelocity = yaw * Vector3.right * p.spin;
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
            TakeSnapshot();
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

            // 止まった判定その2: 速さが閾値近くで揺れていても、位置と向きがほとんど変わらない状態が続く
            if (MovedSinceSnapshot(t)) TakeSnapshot();
            else _stableTimer += dt;

            bool stopped = SimTime > 0.2f && (_slowTimer >= t.stopHoldTime || _stableTimer >= t.stopStableTime);
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
            if (timedOut) Debug.LogWarning("[ThrowSimulator] Timeout. Still moving: " + DescribeMovingBodies());
            LastFallenPins = MolkkyScoring.FallenPins(LastRecord);
            LastScore = MolkkyScoring.ThrowScore(LastFallenPins);
            CurrentState = State.Finished;

            Debug.Log($"[ThrowSimulator] {(timedOut ? "Timeout" : "Stopped")} t={SimTime:F2}s steps={StepCount} " +
                      $"avg={AvgStepMs:F2}ms max={MaxStepMs:F2}ms fallen=[{string.Join(",", LastFallenPins)}] score={LastScore}");
            Finished?.Invoke(LastRecord);
        }

        void TakeSnapshot()
        {
            for (int i = 0; i < MolkkySpec.BodyCount; i++)
            {
                var tr = rig.GetBody(i).transform;
                _snapPos[i] = tr.position;
                _snapRot[i] = tr.rotation;
            }
            _stableTimer = 0f;
        }

        bool MovedSinceSnapshot(PhysicsTuning t)
        {
            for (int i = 0; i < MolkkySpec.BodyCount; i++)
            {
                var rb = rig.GetBody(i);
                if (rb.isKinematic) continue; // 手に持った棒・場外に落ちた物
                var tr = rb.transform;
                if ((tr.position - _snapPos[i]).sqrMagnitude > t.stopMoveTolerance * t.stopMoveTolerance) return true;
                if (Quaternion.Angle(tr.rotation, _snapRot[i]) > t.stopRotateToleranceDeg) return true;
            }
            return false;
        }

        /// <summary>止まった判定の閾値を超えて動いている物（打ち切りの原因の調査用）</summary>
        string DescribeMovingBodies()
        {
            var t = rig.tuning;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < MolkkySpec.BodyCount; i++)
            {
                var rb = rig.GetBody(i);
                if (rb.isKinematic || rb.IsSleeping()) continue;
                float lin = rb.linearVelocity.magnitude, ang = rb.angularVelocity.magnitude;
                if (lin < t.stopLinearSpeed && ang < t.stopAngularSpeed) continue;
                sb.Append(i == MolkkySpec.StickBodyIndex ? "stick" : $"pin{i}");
                sb.Append($"(v={lin:F3} w={ang:F2} pos={rb.position}) ");
            }
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        /// <summary>テストやエディタ用: 止まるまで一気に計算する</summary>
        public void RunToEnd()
        {
            while (CurrentState == State.Simulating) Step(rig.tuning.fixedDeltaTime);
        }
    }
}
