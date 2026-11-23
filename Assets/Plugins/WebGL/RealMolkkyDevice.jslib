mergeInto(LibraryManager.library, {
  // キャンバスの実際のピクセル数 / CSSピクセル数（= 画面の解像度の倍率）
  RealMolkky_CanvasPixelRatio: function () {
    var c = Module['canvas'];
    if (c && c.clientWidth > 0) return c.width / c.clientWidth;
    return window.devicePixelRatio || 1;
  },

  // ブラウザの入力ダイアログで文字を入力してもらう（スマホのキーボードと日本語入力が使える）。
  // キャンセルしたら null
  RealMolkky_Prompt: function (messagePtr, defaultPtr) {
    var r = window.prompt(UTF8ToString(messagePtr), UTF8ToString(defaultPtr));
    if (r === null || r === undefined) return 0;
    var size = lengthBytesUTF8(r) + 1;
    var buf = _malloc(size);
    stringToUTF8(r, buf, size);
    return buf;
  },
});
