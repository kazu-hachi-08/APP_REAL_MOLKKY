using System.Collections.Generic;
using System.Text;
using RealMolkky.Molkky;
using RealMolkky.Play;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RealMolkky.DebugTools
{
    /// <summary>
    /// フェーズ2の投げる操作用の仮UI（IMGUI）。見た目はフェーズ4で作り直す。
    /// ボタンの上で押したときはスワイプを始めないよう、ボタンの範囲を <see cref="ThrowController.IsOverUI"/> に渡す。
    /// </summary>
    public class Phase2ThrowUI : MonoBehaviour
    {
        public const string SandboxScene = "Phase1Sandbox";
        public const string MatchScene = "Phase3Match";

        [SerializeField] MolkkyRig rig;
        [SerializeField] ThrowSimulator simulator;
        [SerializeField] ThrowController controller;
        [SerializeField] Camera cam;

        bool _showTuning;
        bool _showLabels; // 3.5m先だと番号が重なるので既定は非表示
        bool _showDebug = true;
        Vector2 _tuningScroll;
        float _fps, _fpsAccum;
        int _fpsFrames;
        float _scale = 1f;
        GUIStyle _label, _big, _button, _bigButton, _box, _pinLabel, _shadow;

        // ボタンの範囲（GUI座標 = 左上原点・拡大前）
        Rect _leftRect, _rightRect, _styleRect, _menuRect, _tuningRect;
        readonly List<Rect> _blocked = new List<Rect>();

        public void Bind(MolkkyRig r, ThrowSimulator s, ThrowController c, Camera camera)
        {
            rig = r;
            simulator = s;
            controller = c;
            cam = camera;
        }

        void Awake()
        {
            controller.IsOverUI = IsOverUI;
        }

        void Update()
        {
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccum >= 0.5f)
            {
                _fps = _fpsFrames / _fpsAccum;
                _fpsAccum = 0f;
                _fpsFrames = 0;
            }

            ComputeLayout();

            // 立ち位置のボタンは押している間だけ動かす（IMGUI のボタンは押した瞬間しか分からないため自前で判定）
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed && !controller.IsDragging)
            {
                var gp = ToGui(pointer.position.ReadValue());
                if (_leftRect.Contains(gp)) controller.MoveStance(-1f, Time.deltaTime);
                else if (_rightRect.Contains(gp)) controller.MoveStance(1f, Time.deltaTime);
            }
        }

        Vector2 ToGui(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y) / _scale;

        bool IsOverUI(Vector2 screen)
        {
            var gp = ToGui(screen);
            foreach (var r in _blocked)
            {
                if (r.Contains(gp)) return true;
            }
            return false;
        }

        void ComputeLayout()
        {
            _scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 540f);
            float w = Screen.width / _scale;
            float h = Screen.height / _scale;
            const float m = 10f;
            const float bs = 72f;

            _leftRect = new Rect(m, h - bs - m, bs, bs);
            _rightRect = new Rect(m + bs + 8f, h - bs - m, bs, bs);
            _styleRect = new Rect(w - 150f - m, h - bs - m, 150f, bs);
            _menuRect = new Rect(w - 250f - 8f, 6f, 250f, 120f);
            _tuningRect = new Rect(Mathf.Max(4f, w - 250f - 8f - 290f - 6f), 6f, 290f, Mathf.Min(h - 12f - bs - m, 520f));

            _blocked.Clear();
            _blocked.Add(_leftRect);
            _blocked.Add(_rightRect);
            _blocked.Add(_styleRect);
            _blocked.Add(_menuRect);
            if (_showTuning) _blocked.Add(_tuningRect);
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            _big = new GUIStyle(_label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            _bigButton = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            _box = new GUIStyle(GUI.skin.box);
            _pinLabel = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _shadow = new GUIStyle(_label);
        }

        void OnGUI()
        {
            EnsureStyles();
            if (Event.current.type == EventType.Layout) ComputeLayout();
            if (_showLabels) DrawPinLabels();

            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float w = Screen.width / _scale;

            DrawStatus();
            DrawResult(w);
            DrawControls();
            DrawMenu();
            if (_showTuning) DrawTuning();
        }

        void ShadowLabel(Rect r, string text, GUIStyle style)
        {
            _shadow.font = style.font;
            _shadow.fontSize = style.fontSize;
            _shadow.fontStyle = style.fontStyle;
            _shadow.alignment = style.alignment;
            _shadow.wordWrap = style.wordWrap;
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, _shadow);
            GUI.Label(r, text, style);
        }

        void DrawPinLabels()
        {
            bool settled = controller.CurrentState != ThrowController.State.Flying && controller.ThrowCount > 0;
            for (int n = 1; n <= MolkkySpec.PinCount; n++)
            {
                var tr = rig.GetBody(n).transform;
                var top = tr.position + tr.rotation * Vector3.up * MolkkySpec.PinHeight;
                var sp = cam.WorldToScreenPoint(top + Vector3.up * 0.03f);
                if (sp.z <= 0f) continue;
                bool fallen = MolkkySpec.IsPinFallen(tr.position, tr.rotation);
                _pinLabel.normal.textColor = settled && fallen ? new Color(1f, 0.3f, 0.25f) : Color.white;
                ShadowLabel(new Rect(sp.x - 20f, Screen.height - sp.y - 10f, 40f, 20f), n.ToString(), _pinLabel);
            }
        }

        void DrawStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"FPS {_fps:F0}   Throw #{controller.ThrowCount}   {controller.CurrentState}");
            sb.AppendLine($"Style: {controller.Style}   Stance: {controller.Lateral * 100f:+0;-0;0} cm");
            if (_showDebug)
            {
                var v = controller.LastSwipeVelocity;
                if (v != Vector2.zero)
                {
                    sb.AppendLine($"Swipe {v.magnitude:F2} kpx/s  angle {SwipeThrowMapper.SwipeAngleDeg(controller.LastSwipeStroke):+0.0;-0.0}deg" +
                                  (controller.LastReject != SwipeReject.None ? $"  -> {controller.LastReject}" : ""));
                }
                if (controller.ThrowCount > 0)
                {
                    var p = controller.LastParams;
                    sb.AppendLine($"Speed {p.speed:F2}m/s  yaw {p.yawDeg:+0.0;-0.0}deg  spin {p.spin:F1}");
                    sb.AppendLine($"Sim {simulator.SimTime:F2}s  step avg {simulator.AvgStepMs:F2}ms max {simulator.MaxStepMs:F2}ms");
                }
            }
            ShadowLabel(new Rect(8, 6, 440, 120), sb.ToString(), _label);
        }

        void DrawResult(float w)
        {
            if (controller.ThrowCount == 0 || controller.CurrentState == ThrowController.State.Flying) return;
            if (simulator.CurrentState != ThrowSimulator.State.Finished) return;
            string fallen = simulator.LastFallenPins.Count == 0 ? "-" : string.Join(", ", simulator.LastFallenPins);
            string text = $"Score {simulator.LastScore}\nFallen: {fallen}{(simulator.LastTimedOut ? "  (timeout)" : "")}";
            ShadowLabel(new Rect(w * 0.5f - 200f, 40f, 400f, 70f), text, _big);
        }

        void DrawControls()
        {
            bool aiming = controller.CurrentState == ThrowController.State.Aiming;
            GUI.enabled = aiming;
            // 立ち位置は Update で押している間だけ動かす。ここでは見た目だけ
            GUI.Button(_leftRect, "<", _bigButton);
            GUI.Button(_rightRect, ">", _bigButton);
            GUI.enabled = true;

            if (GUI.Button(_styleRect, controller.Style == ThrowStyle.Vertical ? "Vertical\n(1 pin)" : "Horizontal\n(many)", _bigButton))
            {
                controller.ToggleStyle();
            }

            if (controller.ThrowCount == 0 && aiming)
            {
                float w = Screen.width / _scale;
                float h = Screen.height / _scale;
                ShadowLabel(new Rect(w * 0.5f - 300f, h * 0.5f, 600f, 60f),
                    "Swipe up to throw\n< > : move   button: vertical / horizontal", _big);
            }
        }

        void DrawMenu()
        {
            GUILayout.BeginArea(_menuRect);
            GUILayout.BeginVertical(_box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset pins", _button, GUILayout.Height(34))) controller.ResetPins();
            if (GUILayout.Button(_showTuning ? "Tuning <<" : "Tuning >>", _button, GUILayout.Height(34))) _showTuning = !_showTuning;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_showLabels ? "Labels: on" : "Labels: off", _button, GUILayout.Height(30))) _showLabels = !_showLabels;
            if (GUILayout.Button(_showDebug ? "Debug: on" : "Debug: off", _button, GUILayout.Height(30))) _showDebug = !_showDebug;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Match (Phase 3)", _button, GUILayout.Height(30))) SceneManager.LoadScene(MatchScene);
            if (GUILayout.Button("Physics (Phase 1)", _button, GUILayout.Height(30))) SceneManager.LoadScene(SandboxScene);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void DrawTuning()
        {
            GUILayout.BeginArea(_tuningRect);
            GUILayout.BeginVertical(_box);
            _tuningScroll = GUILayout.BeginScrollView(_tuningScroll);

            var t = controller.tuning;
            GUILayout.Label("Swipe", _label);
            t.swipeWindow = Slider("Window s", t.swipeWindow, 0.03f, 0.3f, "F2");
            t.minSwipeSpeed = Slider("Min swipe", t.minSwipeSpeed, 0.05f, 1.5f, "F2");
            t.referenceSwipeSpeed = Slider("Ref swipe", t.referenceSwipeSpeed, 0.3f, 6f, "F2");
            t.verticalBaseSpeed = Slider("Base spd V", t.verticalBaseSpeed, 2f, 9f, "F2");
            t.horizontalBaseSpeed = Slider("Base spd H", t.horizontalBaseSpeed, 2f, 9f, "F2");
            t.speedPerDoubling = Slider("Spd x2 swipe", t.speedPerDoubling, 0f, 3f, "F2");
            t.maxThrowSpeed = Slider("Max speed", t.maxThrowSpeed, 3f, 14f, "F1");
            t.yawPerSwipeDeg = Slider("Yaw/angle", t.yawPerSwipeDeg, 0f, 1f, "F2");
            t.maxYawDeg = Slider("Max yaw", t.maxYawDeg, 1f, 30f, "F1");
            t.faceTarget = GUILayout.Toggle(t.faceTarget, "Face pins from stance", _button);

            GUILayout.Label("Throw", _label);
            t.verticalElevationDeg = Slider("Elev V", t.verticalElevationDeg, 0f, 45f, "F1");
            t.horizontalElevationDeg = Slider("Elev H", t.horizontalElevationDeg, 0f, 45f, "F1");
            t.verticalSpinPerSpeed = Slider("Spin/spd V", t.verticalSpinPerSpeed, -2f, 2f, "F2");
            t.horizontalSpinPerSpeed = Slider("Spin/spd H", t.horizontalSpinPerSpeed, -2f, 2f, "F2");
            t.releaseHeight = Slider("Release h", t.releaseHeight, 0.2f, 1.2f, "F2");

            GUILayout.Label("Stance / camera", _label);
            t.stanceRange = Slider("Stance range", t.stanceRange, 0f, 1.5f, "F2");
            t.stanceMoveSpeed = Slider("Move speed", t.stanceMoveSpeed, 0.1f, 2f, "F2");
            t.cameraHeight = Slider("Cam height", t.cameraHeight, 0.5f, 3f, "F2");
            t.cameraBack = Slider("Cam back", t.cameraBack, 0f, 4f, "F2");
            t.cameraFov = Slider("Cam FOV", t.cameraFov, 20f, 80f, "F0");

            if (GUILayout.Button("Restore defaults", _button)) controller.tuning = new ThrowControlTuning();
            if (GUILayout.Button("Log values", _button)) Debug.Log("[ThrowTuning] " + JsonUtility.ToJson(controller.tuning, true));

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        float Slider(string label, float value, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value.ToString(format)}", _label, GUILayout.Width(130));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true), GUILayout.Height(22));
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
