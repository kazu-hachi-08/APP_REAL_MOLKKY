using System;
using RealMolkky.Molkky;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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

        // 調整値はシーンに保存しない（保存すると古い値がコードの初期値より優先され、直したはずの値が効かなくなる）。
        // 実行中は画面の Tuning から変える
        [NonSerialized] public ThrowControlTuning tuning = new ThrowControlTuning();

        [SerializeField] MolkkyRig rig;
        [SerializeField] ThrowSimulator simulator;
        [SerializeField] ThrowCamera throwCamera;

        /// <summary>止まった後、一定時間で自動的に次の投げに進む（試合では false にして <see cref="BeginNextThrow"/> を呼ぶ）</summary>
        [NonSerialized] public bool autoAdvance = true;

        /// <summary>スワイプ・立ち位置・縦横の操作を受け付けるか（試合の設定画面などでは止める）</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>R キーでピンを並べ直せるか（調整用）</summary>
        [NonSerialized] public bool allowResetKey = true;

        public State CurrentState { get; private set; } = State.Aiming;
        public ThrowStyle Style { get; private set; } = ThrowStyle.Vertical;
        public float Lateral { get; private set; }
        public bool IsDragging => _tracker.IsTracking;
        public int ThrowCount { get; private set; }

        // 直前のスワイプ（調整用の表示）
        public Vector2 LastSwipeVelocity { get; private set; }
        public Vector2 LastSwipeStroke { get; private set; }
        public SwipeReject LastReject { get; private set; }
        public ThrowParams LastParams { get; private set; }

        /// <summary>画面座標（左下原点）がボタンなどの上か。上ならスワイプを始めない</summary>
        public Func<Vector2, bool> IsOverUI { get; set; }

        /// <summary>止まって結果が出たとき</summary>
        public event Action<ThrowSimulator> Settled;

        readonly SwipeTracker _tracker = new SwipeTracker();
        float _settledTimer;
        Pointer _trackingPointer;
        bool _eventSampleSinceUpdate;

        public void Bind(MolkkyRig r, ThrowSimulator s, ThrowCamera c)
        {
            rig = r;
            simulator = s;
            throwCamera = c;
        }

        void OnEnable() => InputSystem.onEvent += OnInputEvent;
        void OnDisable() => InputSystem.onEvent -= OnInputEvent;

        /// <summary>
        /// 指（マウス）の動きを入力イベントごとに、イベントの時刻つきで記録する。
        /// 画面の更新ごとに読むと、描画が一瞬止まったときに「時間は進んだのに動いていない」ことになり、
        /// 速さ（＝力）を低く読んでしまうため。
        /// </summary>
        void OnInputEvent(InputEventPtr e, InputDevice device)
        {
            if (!_tracker.IsTracking || device != _trackingPointer) return;
            if (!e.IsA<StateEvent>() && !e.IsA<DeltaStateEvent>()) return;
            // タッチ画面はイベントの形式が違って読めないことがある。その場合は Update で読む
            if (!_trackingPointer.position.ReadValueFromEvent(e, out var pos)) return;
            _tracker.Add(e.time, pos);
            _eventSampleSinceUpdate = true;
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
            _trackingPointer = null;
            simulator.ResetPins();
            EnterAiming();
        }

        /// <summary>止まった後に次の投げの準備に進む（<see cref="autoAdvance"/> が false のとき用）</summary>
        public void BeginNextThrow()
        {
            _tracker.Cancel();
            _trackingPointer = null;
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
            if (!InputEnabled || CurrentState != State.Aiming || IsDragging) return;
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
                // 倒れたピンはそのまま次の投げに進む（試合では MatchController が立て直してから進める）
                if (autoAdvance && _settledTimer >= tuning.resultHoldTime) EnterAiming();
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
            if (!InputEnabled) return;
            if (kb.spaceKey.wasPressedThisFrame) ToggleStyle();
            if (allowResetKey && kb.rKey.wasPressedThisFrame) ResetPins();
        }

        void HandlePointer()
        {
            var pointer = _tracker.IsTracking ? _trackingPointer : Pointer.current;
            if (pointer == null || !pointer.added)
            {
                _tracker.Cancel();
                _trackingPointer = null;
                return;
            }

            // 入力イベントと同じ時間軸の現在時刻
            double now = InputState.currentTime;
            var pos = pointer.position.ReadValue();
            var press = pointer.press;
            bool gotEvents = _eventSampleSinceUpdate;
            _eventSampleSinceUpdate = false;

            if (!_tracker.IsTracking)
            {
                if (press.wasPressedThisFrame && InputEnabled && CurrentState == State.Aiming && !(IsOverUI?.Invoke(pos) ?? false))
                {
                    _tracker.Begin(now, pos);
                    _trackingPointer = pointer;
                }
                return;
            }

            if (press.isPressed && !press.wasReleasedThisFrame)
            {
                // イベントから読めなかったときだけ、ここで位置を足す
                if (!gotEvents && pos != _tracker.LastPosition) _tracker.Add(now, pos);
                return;
            }

            _tracker.End(now, pos);
            _trackingPointer = null;
            Release();
        }

        void Release()
        {
            // 画面のピクセル → 1000 CSSピクセル（端末によらずだいたい同じ物理的な長さ）
            float unit = 1000f * ScreenScale.PixelsPerCssPixel;
            var vel = _tracker.VelocityAtEnd(tuning.swipeWindow) / unit;
            var stroke = _tracker.StrokeVector() / unit;
            LastSwipeVelocity = vel;
            LastSwipeStroke = stroke;
            LastReject = SwipeThrowMapper.TryMap(vel, stroke, Style, Lateral, tuning, out var p);
            if (LastReject != SwipeReject.None || !InputEnabled) return;
            Throw(p);
        }

        /// <summary>決まった条件で投げる（スワイプから、またはテスト用）。投げる準備中でなければ何もしない</summary>
        public bool Throw(in ThrowParams p)
        {
            if (CurrentState != State.Aiming) return false;
            _tracker.Cancel();
            _trackingPointer = null;
            LastParams = p;
            ThrowCount++;
            CurrentState = State.Flying;
            simulator.Throw(p);
            return true;
        }
    }
}
