using System.Runtime.InteropServices;

namespace RealMolkky.Play
{
    /// <summary>
    /// 文字の入力。WebGL ではブラウザの入力ダイアログを使う（スマホのキーボードと日本語入力がそのまま使えるため）。
    /// </summary>
    public static class TextPrompt
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string RealMolkky_Prompt(string message, string defaultValue);
        public static bool IsSupported => true;
#else
        public static bool IsSupported => false;
#endif

        /// <summary>入力された文字（キャンセルなら null）。対応していない環境では常に null</summary>
        public static string Ask(string message, string defaultValue)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return RealMolkky_Prompt(message, defaultValue ?? string.Empty);
#else
            return null;
#endif
        }
    }
}
