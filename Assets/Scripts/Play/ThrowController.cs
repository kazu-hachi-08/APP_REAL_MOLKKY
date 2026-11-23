using System;
using RealMolkky.Molkky;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealMolkky.Play
{
    /// <summary>
    /// 投げる操作: スワイプ（タッチ／マウスのドラッグ）で投げ、縦横の切り替えと立ち位置の移動を受け付ける。
    /// 物理は <see cref="ThrowSimulator"/> が計算する。
    /// </summary>
    public class ThrowController : MonoBehaviour
    {
        public enum State
        {
            /// <summary>棒を持って投げる準備中（スワイプ・立ち位置・縦横を受け付ける）</summary>
            Aiming,
            /// <summary>投げた後、物理を計算中</summary>
            Flying,
            /// <summary>止まった。結果を見せて少し待つ</summary>
            Settled,
        }

        public ThrowControlTuning tuning = new ThrowControlTuning();

        [SerializeField] MolkkyRig rig;
        [SerializeField] ThrowSimulator simulator;
        [SerializeField] ThrowCamera throwCamera;

        public State CurrentState { get; private set; } = State.Aiming;
        public ThrowStyle Style { get; private set; } = ThrowStyle.Vertical;
        public float Lateral { get; private set; }
        public bool IsDragging => _tracker.IsTracking;
        public int ThrowCount { get; private set; }

        // 直前のスワイプ（調整用の表示）
        public Vector2 LastSwipeVelocity { get; private set; }
        public SwipeReject LastReject { get; private set; }
        public ThrowParams LastParams { get; private set; }

        /// <summary>画面座標（左下原点）がボタンなどの上か。上ならスワイプを始めない</summary>
        public Func<Vector2, bool> IsOverUI { get; set; }

        /// <summary>止まって結果が出たとき</summary>
        public event Action<ThrowSimulator> Settled;

        readonly SwipeTracker _tracker = new SwipeTracker();
        float _settledTimer;

        public void Bind(MolkkyRig r, ThrowSimulator s, ThrowCamera c)
        {
            rig = r;
            simulator = s;
            throwCamera = c;
        }

        void Start()
        {
            simulator.Finished += _ => OnSimFinished();
            throwCamera.Tuning = tuning;
            ResetPins();
            throwCamera.SnapToTarget();
        }

        public void ResetPins()
        {
            _tracker.Cancel();
            simulator.ResetPins();
            EnterAiming();
        }

        public void ToggleStyle() => SetStyle(Style == ThrowStyle.Vertical ? ThrowStyle.Horizontal : ThrowStyle.Vertical);

        public void SetStyle(ThrowStyle style)
        {
            Style = style;
            if (CurrentState == State.Aiming) HoldStick();
        }

        /// <summary>立ち位置を動かす（-1 = 左, +1 = 右）。投げる準備中だけ</summary>
        public void MoveStance(float direction, float deltaTime)
        {
            if (CurrentState != State.Aiming || IsDragging) return;
            SetLateral(Lateral + direction * tuning.stanceMoveSpeed * deltaTime);
        }

        public void SetLateral(float lateral)
        {
            Lateral = Mathf.Clamp(lateral, -tuning.stanceRange, tuning.stanceRange);
            throwCamera.Lateral = Lateral;
            if (CurrentState == State.Aiming) HoldStick();
        }

        void EnterAiming()
        {
            CurrentState = State.Aiming;
            SetLateral(Lateral); // 幅の調整値が変わっていても収める
            HoldStick();
        }

        void HoldStick()
        {
            SwipeThrowMapper.HoldPose(Style, Lateral, tuning, out var pos, out var rot);
            rig.HoldStick(pos, rot);
        }

        void OnSimFinished()
        {
            if (CurrentState != State.Flying) return;
            CurrentState = State.Settled;
            _settledTimer = 0f;
            Settled?.Invoke(simulator);
        }

        void Update()
        {
            throwCamera.Tuning = tuning;
            HandleKeyboard();
            HandlePointer();

            // 調整値（手を離す高さなど）を変えたらすぐ持ち方に反映する
            if (CurrentState == State.Aiming) HoldStick();

            if (CurrentState == State.Settled)
            {
                _settledTimer += Time.deltaTime;
                // 倒れたピンはそのまま。次の投げに進む（立て直しはフェーズ3）
                if (_settledTimer >= tuning.resultHoldTime) EnterAiming();
            }
        }

        void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            float dir = 0f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dir -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir += 1f;
            if (dir != 0f) MoveStance(dir, Time.deltaTime);
            if (kb.spaceKey.wasPressedThisFrame) ToggleStyle();
            if (kb.rKey.wasPressedThisFrame) ResetPins();
        }

        void HandlePointer()
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                _tracker.Cancel();
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            var pos = pointer.position.ReadValue();
            var press = pointer.press;

            if (!_tracker.IsTracking)
            {
                if (press.wasPressedThisFrame && CurrentState == State.Aiming && !(IsOverUI?.Invoke(pos) ?? false))
                {
                    _tracker.Begin(now, pos);
                }
                return;
            }

            if (press.isPressed && !press.wasReleasedThisFrame)
            {
                _tracker.Add(now, pos);
                return;
            }

            _tracker.End(now, pos);
            Release();
        }

        void Release()
        {
            float unit = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
            var vel = _tracker.VelocityAtEnd(tuning.swipeWindow) / unit;
            LastSwipeVelocity = vel;
            LastReject = SwipeThrowMapper.TryMap(vel, Style, Lateral, tuning, out var p);
            if (LastReject != SwipeReject.None || CurrentState != State.Aiming) return;

            LastParams = p;
            ThrowCount++;
            CurrentState = State.Flying;
            simulator.Throw(p);
        }
    }
}
