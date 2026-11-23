using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RealMolkky.Molkky;
using RealMolkky.Play;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RealMolkky.DebugTools
{
    /// <summary>
    /// フェーズ3の1台で対戦する画面の仮UI（IMGUI）。見た目はフェーズ4で作り直す。
    /// 設定画面（名前・個人戦/チーム戦）→ 試合中のスコア表示と操作ボタン → 結果 → 試合終了。
    /// </summary>
    public class Phase3MatchUI : MonoBehaviour
    {
        public const string PracticeScene = "Phase2Throw";
        const int MaxNameLength = 10;

        [SerializeField] MatchController match;
        [SerializeField] ThrowController controller;

        // 設定画面
        MatchMode _mode = MatchMode.Individual;
        int _playerCount = 2;
        readonly string[] _individualNames = new string[MolkkyMatch.MaxPlayers];
        readonly string[] _teamNames = new string[MolkkyMatch.TeamSize * MolkkyMatch.TeamCount];
        Vector2 _setupScroll;

        bool _showMenu;
        float _scale = 1f;
        GUIStyle _label, _small, _big, _huge, _button, _bigButton, _box, _shadow, _row;

        // ボタンの範囲（GUI座標 = 左上原点・拡大前）
        Rect _leftRect, _rightRect, _styleRect, _menuButtonRect, _menuRect;
        readonly List<Rect> _blocked = new List<Rect>();

        public void Bind(MatchController m, ThrowController c)
        {
            match = m;
            controller = c;
        }

        void Awake()
        {
            controller.IsOverUI = IsOverUI;
            LoadSettings();
        }

        // ---- 設定の保存（次に開いたときに名前を入れ直さなくてよいように） ----

        void LoadSettings()
        {
            for (int i = 0; i < _individualNames.Length; i++)
                _individualNames[i] = PlayerPrefs.GetString($"match.name.{i}", $"Player {i + 1}");
            for (int i = 0; i < _teamNames.Length; i++)
                _teamNames[i] = PlayerPrefs.GetString($"match.team.{i}", $"Player {i + 1}");
            _mode = PlayerPrefs.GetInt("match.mode", 0) == 1 ? MatchMode.Team : MatchMode.Individual;
            _playerCount = Mathf.Clamp(PlayerPrefs.GetInt("match.count", 2), 1, MolkkyMatch.MaxPlayers);
        }

        void SaveSettings()
        {
            for (int i = 0; i < _individualNames.Length; i++) PlayerPrefs.SetString($"match.name.{i}", _individualNames[i]);
            for (int i = 0; i < _teamNames.Length; i++) PlayerPrefs.SetString($"match.team.{i}", _teamNames[i]);
            PlayerPrefs.SetInt("match.mode", _mode == MatchMode.Team ? 1 : 0);
            PlayerPrefs.SetInt("match.count", _playerCount);
            PlayerPrefs.Save();
        }

        /// <summary>前後の空白を除き、表示を崩す記号を消して長さを抑える。空なら元の名前</summary>
        static string CleanName(string input, string fallback)
        {
            if (input == null) return fallback;
            var s = input.Replace("<", "").Replace(">", "").Replace("\n", " ").Trim();
            if (s.Length == 0) return fallback;
            var si = new StringInfo(s);
            return si.LengthInTextElements > MaxNameLength ? si.SubstringByTextElements(0, MaxNameLength) : s;
        }

        // ---- 入力 ----

        void Update()
        {
            ComputeLayout();
            var pointer = Pointer.current;
            if (pointer == null) return;

            var phase = match.CurrentPhase;
            if (phase == MatchController.Phase.Playing && pointer.press.isPressed && !controller.IsDragging)
            {
                // 立ち位置のボタンは押している間だけ動かす（IMGUI のボタンは押した瞬間しか分からないため自前で判定）
                var gp = ToGui(pointer.position.ReadValue());
                if (_leftRect.Contains(gp)) controller.MoveStance(-1f, Time.deltaTime);
                else if (_rightRect.Contains(gp)) controller.MoveStance(1f, Time.deltaTime);
            }
            else if (phase == MatchController.Phase.ShowingResult && pointer.press.wasPressedThisFrame &&
                     !IsOverUI(pointer.position.ReadValue()))
            {
                match.SkipResult();
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
            _menuButtonRect = new Rect(w - 80f - 6f, 6f, 80f, 36f);
            _menuRect = new Rect(w - 220f - 6f, 48f, 220f, 130f);

            _blocked.Clear();
            if (match.CurrentPhase == MatchController.Phase.Playing || match.CurrentPhase == MatchController.Phase.ShowingResult)
            {
                _blocked.Add(_leftRect);
                _blocked.Add(_rightRect);
                _blocked.Add(_styleRect);
                _blocked.Add(_menuButtonRect);
                if (_showMenu) _blocked.Add(_menuRect);
            }
        }

        // ---- 描画 ----

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, normal = { textColor = Color.white } };
            _small = new GUIStyle(_label) { fontSize = 13 };
            _row = new GUIStyle(_label) { fontSize = 17, wordWrap = false, clipping = TextClipping.Clip };
            _big = new GUIStyle(_label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _huge = new GUIStyle(_big) { fontSize = 34 };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            _bigButton = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            _shadow = new GUIStyle(_label);
        }

        void OnGUI()
        {
            EnsureStyles();
            if (Event.current.type == EventType.Layout) ComputeLayout();
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float w = Screen.width / _scale;
            float h = Screen.height / _scale;

            switch (match.CurrentPhase)
            {
                case MatchController.Phase.Setup:
                    DrawSetup(w, h);
                    break;
                case MatchController.Phase.Playing:
                    DrawScoreboard();
                    DrawTurnBanner(w);
                    DrawControls(w, h);
                    DrawMenu();
                    break;
                case MatchController.Phase.ShowingResult:
                    DrawScoreboard();
                    DrawResult(w, h);
                    DrawMenu();
                    break;
                case MatchController.Phase.GameOver:
                    DrawScoreboard();
                    DrawGameOver(w, h);
                    break;
            }
        }

        void ShadowLabel(Rect r, string text, GUIStyle style)
        {
            _shadow.font = style.font;
            _shadow.fontSize = style.fontSize;
            _shadow.fontStyle = style.fontStyle;
            _shadow.alignment = style.alignment;
            _shadow.wordWrap = style.wordWrap;
            _shadow.richText = true;
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), StripColors(text), _shadow);
            GUI.Label(r, text, style);
        }

        /// <summary>影は黒一色にしたいので色だけ外す（大きさや太字は残して本文と位置を揃える）</summary>
        static string StripColors(string s) => System.Text.RegularExpressions.Regex.Replace(s, "</?color[^>]*>", "");

        // ---- 設定画面 ----

        void DrawSetup(float w, float h)
        {
            float pw = Mathf.Min(w - 20f, 460f);
            var area = new Rect((w - pw) * 0.5f, 10f, pw, h - 20f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f));
            _setupScroll = GUILayout.BeginScrollView(_setupScroll);

            GUILayout.Label("Real Molkky", _huge, GUILayout.Height(46));
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_mode == MatchMode.Individual, "Individual", _bigButton, GUILayout.Height(48))) _mode = MatchMode.Individual;
            if (GUILayout.Toggle(_mode == MatchMode.Team, "Team (2 vs 2)", _bigButton, GUILayout.Height(48))) _mode = MatchMode.Team;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            if (_mode == MatchMode.Individual)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Players", _label, GUILayout.Width(80), GUILayout.Height(44));
                if (GUILayout.Button("-", _bigButton, GUILayout.Width(56), GUILayout.Height(44))) _playerCount = Mathf.Max(1, _playerCount - 1);
                GUILayout.Label($"<b>{_playerCount}</b>", new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(50), GUILayout.Height(44));
                if (GUILayout.Button("+", _bigButton, GUILayout.Width(56), GUILayout.Height(44))) _playerCount = Mathf.Min(MolkkyMatch.MaxPlayers, _playerCount + 1);
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
                for (int i = 0; i < _playerCount; i++) NameRow($"{i + 1}", _individualNames, i, $"Player {i + 1}");
            }
            else
            {
                for (int t = 0; t < MolkkyMatch.TeamCount; t++)
                {
                    GUILayout.Label($"<b>Team {t + 1}</b>", _label);
                    for (int m = 0; m < MolkkyMatch.TeamSize; m++)
                    {
                        int i = t * MolkkyMatch.TeamSize + m;
                        NameRow("", _teamNames, i, $"Player {i + 1}");
                    }
                    GUILayout.Space(4);
                }
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Start", _bigButton, GUILayout.Height(60))) StartMatch();
            GUILayout.Label("Throw order is decided at random.  50 exactly wins, over 50 drops to 25, 3 misses in a row is out.", new GUIStyle(_small) { wordWrap = true });
            GUILayout.Space(6);
            if (GUILayout.Button("Throw practice / tuning (Phase 2)", _button, GUILayout.Height(36))) SceneManager.LoadScene(PracticeScene);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void NameRow(string label, string[] names, int index, string fallback)
        {
            GUILayout.BeginHorizontal();
            if (label.Length > 0) GUILayout.Label(label, new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(30), GUILayout.Height(46));
            if (TextPrompt.IsSupported)
            {
                // ブラウザの入力ダイアログ（スマホのキーボード・日本語入力が使える）
                if (GUILayout.Button(names[index], _bigButton, GUILayout.Height(46)))
                {
                    names[index] = CleanName(TextPrompt.Ask("Name", names[index]), names[index]);
                }
            }
            else
            {
                var field = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
                string edited = GUILayout.TextField(names[index], MaxNameLength, field, GUILayout.Height(46));
                if (edited != names[index]) names[index] = edited.Replace("<", "").Replace(">", "");
            }
            GUILayout.EndHorizontal();
        }

        void StartMatch()
        {
            List<string> names;
            if (_mode == MatchMode.Individual)
            {
                names = new List<string>();
                for (int i = 0; i < _playerCount; i++) names.Add(CleanName(_individualNames[i], $"Player {i + 1}"));
            }
            else
            {
                names = _teamNames.Select((n, i) => CleanName(n, $"Player {i + 1}")).ToList();
            }
            SaveSettings();
            _showMenu = false;
            match.StartMatch(_mode, names);
        }

        // ---- 試合中 ----

        void DrawScoreboard()
        {
            var m = match.Match;
            if (m == null) return;
            bool team = m.Mode == MatchMode.Team;
            float rowH = team ? 44f : 26f;
            var box = new Rect(6f, 6f, 270f, 12f + rowH * m.Sides.Count);
            GUI.Box(box, GUIContent.none);
            GUI.Box(box, GUIContent.none); // 2回描いて背景を濃くする

            for (int i = 0; i < m.Sides.Count; i++)
            {
                var s = m.Sides[i];
                bool current = !m.IsOver && i == m.CurrentSideIndex;
                bool winner = m.IsOver && i == m.WinnerIndex;
                string color = s.Disqualified ? "#888888" : winner ? "#ffd84a" : current ? "#ffe680" : "#ffffff";
                string marker = current ? "> " : winner ? "* " : "  ";
                string misses = s.Disqualified ? "OUT" : new string('x', s.Misses);
                var r = new Rect(box.x + 8f, box.y + 6f + rowH * i, box.width - 16f, rowH);

                GUI.Label(new Rect(r.x, r.y, 170f, 26f), $"<color={color}>{marker}<b>{s.Name}</b></color>", _row);
                GUI.Label(new Rect(r.x + 170f, r.y, 44f, 26f), $"<color={color}><b>{s.Score}</b></color>", new GUIStyle(_row) { alignment = TextAnchor.UpperRight });
                GUI.Label(new Rect(r.x + 222f, r.y, 40f, 26f), $"<color=#ff6a5a>{misses}</color>", _row);
                if (team)
                {
                    var sb = new StringBuilder("    ");
                    for (int k = 0; k < s.Members.Count; k++)
                    {
                        if (k > 0) sb.Append(" / ");
                        bool next = current && k == s.NextMemberIndex;
                        sb.Append(next ? $"<color=#ffe680><b>{s.Members[k]}</b></color>" : s.Members[k]);
                    }
                    GUI.Label(new Rect(r.x, r.y + 20f, r.width, 22f), sb.ToString(), _small);
                }
            }
        }

        void DrawTurnBanner(float w)
        {
            var m = match.Match;
            var side = m.CurrentSide;
            string who = m.Mode == MatchMode.Team ? $"{m.CurrentThrower}'s turn  <size=16>({side.Name})</size>" : $"{m.CurrentThrower}'s turn";
            string sub = $"{side.Score} pts  -  needs {MolkkyMatch.TargetScore - side.Score}";
            if (side.Misses > 0) sub += $"   <color=#ff8a7a>misses {side.Misses}/{MolkkyMatch.MaxConsecutiveMisses}</color>";

            float y = 6f + (m.Mode == MatchMode.Team ? 44f : 26f) * m.Sides.Count + 18f;
            bool wide = w >= 760f;
            var r = wide ? new Rect(290f, 8f, w - 290f - 100f, 70f) : new Rect(10f, y, w - 20f, 70f);
            ShadowLabel(r, $"{who}\n<size=16>{sub}</size>", _big);

            if (controller.CurrentState == ThrowController.State.Aiming && m.ThrowCount == 0)
            {
                float h = Screen.height / _scale;
                ShadowLabel(new Rect(w * 0.5f - 300f, h * 0.55f, 600f, 60f),
                    "Swipe up to throw\n< > : move   button: vertical / horizontal", new GUIStyle(_big) { fontSize = 18 });
            }
        }

        void DrawControls(float w, float h)
        {
            bool aiming = controller.CurrentState == ThrowController.State.Aiming && controller.InputEnabled;
            GUI.enabled = aiming;
            // 立ち位置は Update で押している間だけ動かす。ここでは見た目だけ
            GUI.Button(_leftRect, "<", _bigButton);
            GUI.Button(_rightRect, ">", _bigButton);
            if (GUI.Button(_styleRect, controller.Style == ThrowStyle.Vertical ? "Vertical\n(1 pin)" : "Horizontal\n(many)", _bigButton))
            {
                controller.ToggleStyle();
            }
            GUI.enabled = true;
        }

        void DrawResult(float w, float h)
        {
            var r = match.LastResult;
            var m = match.Match;
            var sb = new StringBuilder();
            string fallen = match.LastFallenPins.Count == 0 ? "" : $"<size=18>Fallen: {string.Join(", ", match.LastFallenPins)}</size>\n";

            if (r.missed)
            {
                sb.Append($"{r.thrower}: <color=#ff8a7a>Miss!</color>\n");
                sb.Append(r.disqualified
                    ? $"<color=#ff6a5a>{MolkkyMatch.MaxConsecutiveMisses} misses in a row - {m.Sides[r.sideIndex].Name} is out</color>"
                    : $"<size=18>{r.misses} in a row ({MolkkyMatch.MaxConsecutiveMisses} = out)</size>");
            }
            else
            {
                sb.Append($"{r.thrower}: <color=#ffe680>+{r.points}</color>\n");
                sb.Append(fallen);
                if (r.overflow)
                    sb.Append($"<color=#ff8a7a>{r.scoreBefore} + {r.points} = {r.scoreBefore + r.points}, over {MolkkyMatch.TargetScore}!  Back to {MolkkyMatch.OverflowResetScore}</color>");
                else
                    sb.Append($"{r.scoreBefore} -> <b>{r.scoreAfter}</b>");
            }
            if (r.matchOver) sb.Append("\n<color=#ffd84a>Game over!</color>");

            var rect = new Rect(w * 0.5f - 260f, h * 0.30f, 520f, 200f);
            ShadowLabel(rect, sb.ToString(), _big);
            ShadowLabel(new Rect(w * 0.5f - 200f, h - 50f, 400f, 30f), "tap to continue", new GUIStyle(_small) { alignment = TextAnchor.UpperCenter });
        }

        void DrawGameOver(float w, float h)
        {
            var m = match.Match;
            float pw = Mathf.Min(w - 20f, 420f);
            float ph = 150f + 30f * m.Sides.Count;
            var area = new Rect((w - pw) * 0.5f, Mathf.Max(10f, (h - ph) * 0.5f), pw, ph);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));

            string title;
            if (m.WinnerIndex < 0) title = "No winner";
            else
            {
                var ws = m.Sides[m.WinnerIndex];
                title = m.Mode == MatchMode.Team ? $"{ws.Name} wins!\n<size=16>{string.Join(" / ", ws.Members)}</size>" : $"{ws.Name} wins!";
            }
            GUILayout.Label($"<color=#ffd84a>{title}</color>", _big);
            GUILayout.Space(6);

            // 失格していない人を点の高い順、失格した人は最後
            var ranking = m.Sides.Select((s, i) => (s, i))
                .OrderBy(x => x.i == m.WinnerIndex ? 0 : 1)
                .ThenBy(x => x.s.Disqualified ? 1 : 0)
                .ThenByDescending(x => x.s.Score);
            foreach (var (s, _) in ranking)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(s.Name, _row);
                GUILayout.FlexibleSpace();
                GUILayout.Label(s.Disqualified ? $"<color=#888888>OUT ({s.Score})</color>" : $"<b>{s.Score}</b>", _row);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label($"<size=13>{m.ThrowCount} throws</size>", _small);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rematch", _bigButton, GUILayout.Height(52))) match.Rematch();
            if (GUILayout.Button("New game", _bigButton, GUILayout.Height(52))) match.BackToSetup();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawMenu()
        {
            if (GUI.Button(_menuButtonRect, _showMenu ? "Close" : "Menu", _button)) _showMenu = !_showMenu;
            if (!_showMenu) return;
            GUILayout.BeginArea(_menuRect);
            GUILayout.BeginVertical(_box);
            if (GUILayout.Button("Restart (same players)", _button, GUILayout.Height(36)))
            {
                _showMenu = false;
                match.Rematch();
            }
            if (GUILayout.Button("Quit match", _button, GUILayout.Height(36)))
            {
                _showMenu = false;
                match.BackToSetup();
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
