/*
 * Unfold <-> Piskel bridge.
 *
 * Piskel 소스는 한 줄도 고치지 않는다. 이 파일이 WKUserScript로 주입되어
 * 저장 버튼을 달고, 프레임을 한 장의 스프라이트 시트로 합성해 네이티브로
 * 넘긴다. 저장에 의존하는 Piskel API는 아래와 같고, 벤더링된 빌드가
 * 고정돼 있다. 재벤더링 시 다음 표면과 아래 레이아웃 어댑터를 확인한다:
 *
 *   pskl.app.piskelController.getFrameCount / renderFrameAt /
 *     getWidth / getHeight / getFPS / serialize / setPiskel
 *   pskl.utils.serialization.Deserializer.deserialize
 */
(function () {
  "use strict";

  var SAVE_BUTTON_ID = "unfold-save-button";
  var ERROR_ID = "unfold-error";

  /* Piskel은 DOM ready 이후에 스스로를 초기화한다. 주입 시점이 그보다
   * 이를 수 있으므로 컨트롤러가 생길 때까지 기다린다. */
  function whenPiskelReady(callback) {
    if (window.pskl && pskl.app && pskl.app.piskelController && pskl.utils) {
      callback();
      return;
    }
    window.setTimeout(function () { whenPiskelReady(callback); }, 100);
  }

  function post(message) {
    window.webkit.messageHandlers.unfold.postMessage(JSON.stringify(message));
  }

  /* 프레임을 가로 1행으로 이어 붙인다. 시트 인덱스가 Piskel의 프레임
   * 순서와 1:1이라 매니페스트의 frames 배열이 그냥 [0..n-1]이 된다. */
  function buildSheetDataURL(controller) {
    var frameCount = controller.getFrameCount();
    var width = controller.getWidth();
    var height = controller.getHeight();

    var canvas = document.createElement("canvas");
    canvas.width = width * frameCount;
    canvas.height = height;
    var context = canvas.getContext("2d");

    for (var i = 0; i < frameCount; i++) {
      /* renderFrameAt already returns a canvas with the layers composited. */
      var frame = controller.renderFrameAt(i, true);
      context.drawImage(frame, i * width, 0);
    }
    return canvas.toDataURL("image/png");
  }

  /* 완전히 투명한 size x size 스프라이트 한 장짜리 .piskel 문서.
   * Piskel의 Serializer가 내는 것과 같은 모양이다: layers는 JSON 문자열
   * 배열이고, 청크는 {layout, base64PNG}. */
  function blankPiskelJSON(size) {
    var canvas = document.createElement("canvas");
    canvas.width = size;
    canvas.height = size;

    var layer = JSON.stringify({
      name: "Layer 1",
      opacity: 1,
      frameCount: 1,
      chunks: [{ layout: [[0]], base64PNG: canvas.toDataURL() }]
    });

    return JSON.stringify({
      modelVersion: 2,
      piskel: {
        name: "Unfold Character",
        description: "",
        fps: 12,
        width: size,
        height: size,
        layers: [layer]
      }
    });
  }

  function loadDocument(json, done) {
    /* A malformed source.piskel (a hostile imported package, say) must not
     * abort init: fall back to a blank document but still call done() so
     * the save button gets installed. */
    var parsed;
    try {
      parsed = JSON.parse(json);
    } catch (error) {
      window.console.error("Unfold: the saved document isn't valid JSON, starting a blank one", error);
      parsed = JSON.parse(blankPiskelJSON(64));
    }

    try {
      pskl.utils.serialization.Deserializer.deserialize(
        parsed,
        function (piskel) {
          pskl.app.piskelController.setPiskel(piskel);
          done();
        },
        function (error) {
          window.console.error("Unfold: could not load the saved document", error);
          done();
        }
      );
    } catch (error) {
      window.console.error("Unfold: the deserializer threw, starting with an empty editor", error);
      done();
    }
  }

  function save() {
    var button = document.getElementById(SAVE_BUTTON_ID);
    if (button) { button.disabled = true; }
    clearError();

    /* Everything past this point can throw (Piskel internals, a null 2d
     * context). Without the catch, the button stays disabled forever and
     * nothing is posted. */
    try {
      var controller = pskl.app.piskelController;
      var init = window.__unfoldInit || {};

      post({
        type: "save",
        width: controller.getWidth(),
        height: controller.getHeight(),
        fps: controller.getFPS(),
        frameCount: controller.getFrameCount(),
        sheetPNG: buildSheetDataURL(controller),
        piskelJSON: controller.serialize(),
        characterID: init.characterID || null
      });
    } catch (error) {
      window.console.error("Unfold: could not prepare the drawing for saving", error);
      window.unfoldBridge.saveFailed("The editor couldn't prepare the drawing. Try again.");
    }
  }

  function clearError() {
    var existing = document.getElementById(ERROR_ID);
    if (existing) { existing.remove(); }
  }

  function installButton(label) {
    var button = document.createElement("button");
    button.id = SAVE_BUTTON_ID;
    button.type = "button";
    button.textContent = label;
    button.addEventListener("click", save);
    document.body.appendChild(button);
  }

  /* 네이티브가 저장에 실패했을 때 부른다. 창은 닫히지 않고 그림도
   * 그대로다 — 사용자가 고쳐서 다시 누르면 된다. */
  window.unfoldBridge = {
    saveFailed: function (message) {
      var button = document.getElementById(SAVE_BUTTON_ID);
      if (button) { button.disabled = false; }
      clearError();

      /* An empty message means "the user just cancelled" — re-enable the
       * button, but don't flash an empty red banner. */
      if (!message) { return; }

      var banner = document.createElement("div");
      banner.id = ERROR_ID;
      banner.textContent = message;
      document.body.appendChild(banner);
    }
  };

  whenPiskelReady(function () {
    /* The consolidated tool rail sits against the right edge, so the delegated
     * tooltips should open toward the workspace (left) rather than off-screen. */
    document.querySelectorAll('#tool-section [rel="tooltip"]').forEach(function (tool) {
      tool.setAttribute("data-placement", "left");
    });

    /* Canvas sizing.
     *
     * Piskel's vendor getAvailableWidth_ subtracts #tool-section AND
     * #application-action-section as if the two rails sit side by side. The
     * Unfold consolidation stacks them into a single right-edge rail
     * (var(--u-rail-width) = 112px), so the vendor value is one rail too small
     * and the canvas renders collapsed (Piskel then paints the "zoomed out"
     * #A0A0A0 fill). Add one rail back.
     *
     * Do NOT rebuild this from `.main-column` measurements: that box is
     * `flex:1; min-width:0` and momentarily resolves to 0 during window
     * occlusion / Space switches / reflow. Piskel's ResizeObserver on
     * #main-wrapper then runs relayout_() with a ~1px display size, FrameRenderer
     * clamps the zoom, and the drawing flickers out until the next relayout.
     * Deriving from the vendor value (rooted in #main-wrapper, a fixed-inset
     * element that always has a real size) plus a floor clamp avoids both the
     * collapse and the flicker. getAvailableHeight_ is left untouched — vendor
     * already measures #main-wrapper directly and needs no correction. */
    var drawing = pskl.app.drawingController;
    if (drawing && typeof drawing.getAvailableWidth_ === "function") {
      var RAIL_WIDTH = 112;
      var vendorAvailableWidth = drawing.getAvailableWidth_.bind(drawing);
      drawing.getAvailableWidth_ = function () {
        var w = vendorAvailableWidth() + RAIL_WIDTH;
        return w > 80 ? w : 600;
      };
    }

    if (window.Constants) {
      window.Constants.ZOOMED_OUT_BACKGROUND_COLOR = "#171719";
    }

    var init = window.__unfoldInit || {};
    var json = init.piskelJSON || blankPiskelJSON(init.canvasSide || 64);

    loadDocument(json, function () {
      installButton(init.saveButtonLabel || "Save");
    });
  });
})();
