using UnityEngine;

namespace RealMolkky.DebugTools
{
    /// <summary>
    /// フェーズ0の動作確認用。FPS と端末情報を画面左上に表示する。
    /// </summary>
    public class DeviceInfoOverlay : MonoBehaviour
    {
        private float _fps;
        private float _accum;
        private int _frames;
        private GUIStyle _style;

        private void Update()
        {
            _accum += Time.unscaledDeltaTime;
            _frames++;
            if (_accum >= 0.5f)
            {
                _fps = _frames / _accum;
                _accum = 0f;
                _frames = 0;
            }
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(14, Screen.height / 40),
                    normal = { textColor = Color.white },
                };
            }

            string text =
                $"FPS: {_fps:F1}\n" +
                $"Screen: {Screen.width}x{Screen.height} (dpi {Screen.dpi:F0})\n" +
                $"Device: {SystemInfo.deviceType} / {(Application.isMobilePlatform ? "Mobile" : "Desktop")}\n" +
                $"GPU: {SystemInfo.graphicsDeviceName}\n" +
                $"API: {SystemInfo.graphicsDeviceVersion}\n" +
                $"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}\n" +
                $"Unity: {Application.unityVersion}";

            var rect = new Rect(10, 10, Screen.width - 20, Screen.height - 20);
            var shadow = new GUIStyle(_style) { normal = { textColor = Color.black } };
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, shadow);
            GUI.Label(rect, text, _style);
        }
    }
}
