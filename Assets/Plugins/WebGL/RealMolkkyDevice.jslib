mergeInto(LibraryManager.library, {
  // キャンバスの実際のピクセル数 / CSSピクセル数（= 画面の解像度の倍率）
  RealMolkky_CanvasPixelRatio: function () {
    var c = Module['canvas'];
    if (c && c.clientWidth > 0) return c.width / c.clientWidth;
    return window.devicePixelRatio || 1;
  },
});
