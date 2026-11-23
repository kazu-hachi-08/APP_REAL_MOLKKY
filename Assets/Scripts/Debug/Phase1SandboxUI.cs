using System.Text;
using RealMolkky.Molkky;
using UnityEngine;

namespace RealMolkky.DebugTools
{
    /// <summary>
    /// フェーズ1の物理サンドボックス用の仮UI（IMGUI）。
    /// WebGL の IMGUI は日本語フォントがないため、表示は英語にしている。
    /// </summary>
    public class Phase1SandboxUI : MonoBehaviour
    {
        [SerializeField] MolkkyRig rig;
        [SerializeField] ThrowSimulator simulator;
        [SerializeField] ThrowPlayer player;
        [SerializeField] Camera cam;

        public ThrowParams throwParams = ThrowParams.Default;

        static readonly (string name, Vector3 pos, Vector3 lookAt)[] CameraViews =
        {
            ("Thrower", new Vector3(0f, 1.5f, -1.2f), new Vector3(0f, 0.05f, MolkkySpec.ThrowDistance + 0.08f)),
            ("Pins", new Vector3(0.9f, 0.55f, MolkkySpec.ThrowDistance - 0.9f), new Vector3(0f, 0.05f, MolkkySpec.ThrowDistance + 0.1f)),
            ("Side", new Vector3(2.6f, 0.9f, 1.9f), new Vector3(0f, 0.3f, 1.9f)),
        };

        int _view;
        bool _showTuning;
        bool _showLabels = true;
        Vector2 _tuningScroll;
        float _fps, _fpsAccum;
        int _fpsFrames;
        int _lastRecordBytes;
        string _replaySource = "";
        GUIStyle _label, _button, _box, _pinLabel;
        float _scale = 1f;

        public void Bind(MolkkyRig r, ThrowSimulator s, ThrowPlayer p, Camera c)
        {
            rig = r;
            simulator = s;
            player = p;
            cam = c;
        }

        void Start()
        {
            ApplyView();
            simulator.Finished += rec => _lastRecordBytes = rec.ToBytes().Length;
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
        }

        void ApplyView()
        {
            var v = CameraViews[_view];
            cam.transform.position = v.pos;
            cam.transform.LookAt(v.lookAt);
        }

        void DoThrow(ThrowStyle style)
        {
            player.Stop();
            simulator.ResetPins();
            throwParams.style = style;
            simulator.Throw(throwParams);
        }

        void DoReplay(bool viaBytes)
        {
            var rec = simulator.LastRecord;
            if (rec == null || simulator.CurrentState != ThrowSimulator.State.Finished) return;
            // 送受信と同じ経路を通して、バイト列から戻した記録で再生できることを確かめる
            if (viaBytes) rec = ThrowRecord.FromBytes(rec.ToBytes());
            _replaySource = viaBytes ? "bytes" : "memory";
            player.Play(rec);
        }

        void EnsureStyles()
        {
            _scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 540f);
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false, normal = { textColor = Color.white } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            _box = new GUIStyle(GUI.skin.box);
            _pinLabel = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            EnsureStyles();
            if (_showLabels) DrawPinLabels();

            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float w = Screen.width / _scale;
            float h = Screen.height / _scale;

            DrawStatus();
            DrawControls(w, h);
            if (_showTuning) DrawTuning(w, h);
        }

        void DrawPinLabels()
        {
            bool finished = simulator.CurrentState == ThrowSimulator.State.Finished && !player.IsPlaying;
            for (int n = 1; n <= MolkkySpec.PinCount; n++)
            {
                var tr = rig.GetBody(n).transform;
                var top = tr.position + tr.rotation * Vector3.up * MolkkySpec.PinHeight;
                var sp = cam.WorldToScreenPoint(top + Vector3.up * 0.03f);
                if (sp.z <= 0f) continue;

                bool fallen = MolkkySpec.IsPinFallen(tr.position, tr.rotation);
                _pinLabel.normal.textColor = finished && fallen ? new Color(1f, 0.3f, 0.25f) : Color.white;
                var r = new Rect(sp.x - 20f, Screen.height - sp.y - 10f, 40f, 20f);
                GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), n.ToString(), Shadow(_pinLabel));
                GUI.Label(r, n.ToString(), _pinLabel);
            }
        }

        static GUIStyle _shadowCache;
        static GUIStyle Shadow(GUIStyle s)
        {
            _shadowCache ??= new GUIStyle(s);
            _shadowCache.fontSize = s.fontSize;
            _shadowCache.fontStyle = s.fontStyle;
            _shadowCache.alignment = s.alignment;
            _shadowCache.normal.textColor = Color.black;
            return _shadowCache;
        }

        void DrawStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"FPS {_fps:F0}   {(Application.isMobilePlatform ? "Mobile" : "Desktop")}");
            sb.AppendLine($"State: {simulator.CurrentState}{(player.IsPlaying ? $"  (replay {_replaySource} {player.PlayTime:F1}s)" : "")}");
            sb.AppendLine($"Sim {simulator.SimTime:F2}s  steps {simulator.StepCount}  dropped {simulator.DroppedSteps}");
            sb.AppendLine($"Step avg {simulator.AvgStepMs:F2}ms  max {simulator.MaxStepMs:F2}ms");
            if (simulator.LastRecord != null)
            {
                sb.AppendLine($"Record {simulator.LastRecord.FrameCount} frames  {_lastRecordBytes / 1024f:F1} KB");
            }
            if (simulator.CurrentState == ThrowSimulator.State.Finished)
            {
                sb.AppendLine($"Fallen: [{string.Join(", ", simulator.LastFallenPins)}]{(simulator.LastTimedOut ? "  TIMEOUT" : "")}");
                sb.AppendLine($"Score: {simulator.LastScore}");
            }

            string text = sb.ToString();
            var rect = new Rect(8, 6, 420, 160);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, Shadow(_label));
            GUI.Label(rect, text, _label);
        }

        void DrawControls(float w, float h)
        {
            const float pw = 250f;
            float x = w - pw - 8f;
            GUILayout.BeginArea(new Rect(x, 6f, pw, h - 12f));
            GUILayout.BeginVertical(_box);

            bool busy = simulator.CurrentState == ThrowSimulator.State.Simulating;
            GUI.enabled = !busy;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Throw V", _button, GUILayout.Height(40))) DoThrow(ThrowStyle.Vertical);
            if (GUILayout.Button("Throw H", _button, GUILayout.Height(40))) DoThrow(ThrowStyle.Horizontal);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset", _button, GUILayout.Height(32)))
            {
                player.Stop();
                simulator.ResetPins();
            }
            GUI.enabled = !busy && simulator.CurrentState == ThrowSimulator.State.Finished;
            if (GUILayout.Button("Replay", _button, GUILayout.Height(32))) DoReplay(false);
            if (GUILayout.Button("Replay(bytes)", _button, GUILayout.Height(32))) DoReplay(true);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Cam: {CameraViews[_view].name}", _button, GUILayout.Height(30)))
            {
                _view = (_view + 1) % CameraViews.Length;
                ApplyView();
            }
            if (GUILayout.Button(_showLabels ? "Labels: on" : "Labels: off", _button, GUILayout.Height(30))) _showLabels = !_showLabels;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            throwParams.speed = Slider("Speed m/s", throwParams.speed, 1f, 10f, "F2");
            throwParams.elevationDeg = Slider("Elevation deg", throwParams.elevationDeg, 0f, 60f, "F1");
            throwParams.yawDeg = Slider("Yaw deg", throwParams.yawDeg, -10f, 10f, "F1");
            throwParams.spin = Slider("Spin rad/s", throwParams.spin, -20f, 20f, "F1");
            throwParams.lateral = Slider("Lateral m", throwParams.lateral, -0.5f, 0.5f, "F2");
            throwParams.releaseHeight = Slider("Release h m", throwParams.releaseHeight, 0.2f, 1.2f, "F2");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Defaults", _button)) throwParams = ThrowParams.Default;
            if (GUILayout.Button(_showTuning ? "Tuning <<" : "Tuning >>", _button)) _showTuning = !_showTuning;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void DrawTuning(float w, float h)
        {
            const float pw = 270f;
            float x = w - 250f - 8f - pw - 6f;
            GUILayout.BeginArea(new Rect(Mathf.Max(4f, x), 170f, pw, h - 176f));
            GUILayout.BeginVertical(_box);
            _tuningScroll = GUILayout.BeginScrollView(_tuningScroll);

            var t = rig.tuning;
            GUILayout.Label("Applied on next throw / reset", _label);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Step 1/{Mathf.RoundToInt(1f / t.fixedDeltaTime)}", _label, GUILayout.Width(80));
            foreach (int hz in new[] { 60, 90, 120, 180, 240 })
            {
                if (GUILayout.Button(hz.ToString(), _button)) t.fixedDeltaTime = 1f / hz;
            }
            GUILayout.EndHorizontal();

            t.solverIterations = Mathf.RoundToInt(Slider("Solver iter", t.solverIterations, 2, 32, "F0"));
            t.solverVelocityIterations = Mathf.RoundToInt(Slider("Velocity iter", t.solverVelocityIterations, 1, 16, "F0"));
            t.contactOffset = Slider("Contact offset", t.contactOffset, 0.0005f, 0.01f, "F4");
            t.bounceThreshold = Slider("Bounce thresh", t.bounceThreshold, 0.1f, 3f, "F2");
            t.woodDensity = Slider("Wood density", t.woodDensity, 400f, 1000f, "F0");
            t.woodStaticFriction = Slider("Wood fric s", t.woodStaticFriction, 0f, 1.2f, "F2");
            t.woodDynamicFriction = Slider("Wood fric d", t.woodDynamicFriction, 0f, 1.2f, "F2");
            t.woodBounciness = Slider("Wood bounce", t.woodBounciness, 0f, 1f, "F2");
            t.groundStaticFriction = Slider("Grass fric s", t.groundStaticFriction, 0f, 1.5f, "F2");
            t.groundDynamicFriction = Slider("Grass fric d", t.groundDynamicFriction, 0f, 1.5f, "F2");
            t.groundBounciness = Slider("Grass bounce", t.groundBounciness, 0f, 1f, "F2");
            t.groundRollingResistance = Slider("Grass rolling", t.groundRollingResistance, 0f, 15f, "F1");
            t.linearDamping = Slider("Lin damping", t.linearDamping, 0f, 1f, "F2");
            t.angularDamping = Slider("Ang damping", t.angularDamping, 0f, 2f, "F2");
            t.stopLinearSpeed = Slider("Stop lin", t.stopLinearSpeed, 0.005f, 0.1f, "F3");
            t.stopAngularSpeed = Slider("Stop ang", t.stopAngularSpeed, 0.05f, 1f, "F2");
            t.recordEverySteps = Mathf.RoundToInt(Slider("Record every", t.recordEverySteps, 1, 8, "F0"));

            if (GUILayout.Button("Restore defaults", _button)) rig.tuning = new PhysicsTuning();
            if (GUILayout.Button("Log values", _button)) Debug.Log("[Tuning] " + JsonUtility.ToJson(rig.tuning, true));

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
