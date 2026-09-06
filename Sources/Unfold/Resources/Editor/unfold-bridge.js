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
   * 이를 수 있으므로 컨트롤러가 생길 때까지 기다린다. 다만 Piskel이
   * 아예 부팅에 실패하면(자체 unsupported-browser 경로 등) 무한 폴링에
   * 빠지므로, 상한을 두고 그 지점에서 사용자에게 알린다. */
  var PISKEL_READY_MAX_ATTEMPTS = 100; // 100 * 100ms ≈ 10s

  function whenPiskelReady(callback) {
    var attempts = 0;
    (function poll() {
      if (window.pskl && pskl.app && pskl.app.piskelController && pskl.utils) {
        callback();
        return;
      }
      attempts += 1;
      if (attempts >= PISKEL_READY_MAX_ATTEMPTS) {
        window.console.error("Unfold: Piskel did not finish loading");
        showError("The editor didn't finish loading. Close this window and open it again.");
        return;
      }
      window.setTimeout(poll, 100);
    })();
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

  /* Replace any current banner with one showing `message`. An empty/blank
   * message just clears — callers use that to mean "nothing to report". */
  function showError(message) {
    clearError();
    if (!message) { return; }
    var banner = document.createElement("div");
    banner.id = ERROR_ID;
    banner.textContent = message;
    document.body.appendChild(banner);
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

      /* An empty message means "the user just cancelled" — re-enable the
       * button, but don't flash an empty red banner. */
      showError(message);
    }
  };

  /* Piskel's CachedFrameRenderer marks itself clean *before* it paints:
   *
   *   render(frame) {
   *     var key = [...zoom, offset, size, frame.getHash()].join("-");
   *     if (this.serializedFrame != key) { this.serializedFrame = key; ...paint... }
   *   }
   *
   * When that paint doesn't land on the display canvas, the cache still
   * believes the frame is on screen, so every later animation-frame render
   * skips — the sprite stays blank until an edit changes the frame hash again.
   *
   * A single-click erase is exactly one hash change, so it falls straight into
   * the trap: the drawing vanishes and only reappears on the next edit. A drag
   * changes the hash every frame, which is why dragging always looked fine.
   *
   * Clearing the key on the way into every render makes that state
   * unreachable: a paint that didn't land is simply retried next frame.
   */
  function installRenderCacheInvalidation() {
    var drawing = pskl.app.drawingController;
    if (!drawing) { return; }

    var renderers = [drawing.renderer];
    var composite = drawing.compositeRenderer;
    if (composite && composite.renderers) {
      renderers = renderers.concat(composite.renderers);
    }

    renderers.forEach(function (renderer) {
      if (!renderer || typeof renderer.serializedFrame !== "string" ||
          typeof renderer.render !== "function" || renderer.__unfoldUncached) {
        return;
      }
      var original = renderer.render;
      renderer.render = function () {
        /* Clearing the key on the way in means the "already painted" branch is
         * never taken, so a paint that failed to land is always retried on the
         * next animation frame. The drawing surface is one 64x64-ish sprite and
         * Piskel already repaints it on every model change mid-drag, so the
         * extra work is small next to a permanently blank canvas. */
        this.serializedFrame = "";
        return original.apply(this, arguments);
      };
      renderer.__unfoldUncached = true;
    });
  }

  /* Piskel's FrameUtils.drawToCanvas reuses two module-global scratch objects
   * keyed only by sprite size ("64-64"): an ImageData and an offscreen canvas.
   * It fills them with the frame, then composites the offscreen canvas onto the
   * target with drawImage.
   *
   * Every renderer that runs in the same animation frame — the drawing surface,
   * the tool overlay, the layer previews — shares those scratch objects. When
   * WebKit defers the drawImage, a later renderer can refill the shared canvas
   * before the earlier draw is flushed, and the first target ends up with the
   * later renderer's content. After a single-click erase the overlay frame is
   * completely transparent, so the drawing surface inherits "nothing" and the
   * sprite disappears until the next edit repaints it.
   *
   * Reading a pixel back forces the queued work to flush before the shared
   * scratch canvas can be reused. One 1x1 read per blit on a sprite-sized canvas
   * is cheap next to the bug it prevents. */
  function installSharedScratchFlush() {
    var frameUtils = pskl.utils && pskl.utils.FrameUtils;
    if (!frameUtils || typeof frameUtils.drawToCanvas !== "function" ||
        frameUtils.__unfoldFlushed) {
      return;
    }
    var original = frameUtils.drawToCanvas;
    frameUtils.drawToCanvas = function (frame, canvas) {
      var result = original.apply(this, arguments);
      try {
        if (canvas && canvas.width > 0 && canvas.height > 0) {
          canvas.getContext("2d").getImageData(0, 0, 1, 1);
        }
      } catch (error) { /* tainted or zero-sized canvas: nothing to flush */ }
      return result;
    };
    frameUtils.__unfoldFlushed = true;
  }

  whenPiskelReady(function () {
    try { installSharedScratchFlush(); } catch (error) {
      window.console.error("Unfold: could not install the scratch-canvas flush", error);
    }
    try { installRenderCacheInvalidation(); } catch (error) {
      window.console.error("Unfold: could not install the render-cache workaround", error);
    }

    var init = window.__unfoldInit || {};
    var json = init.piskelJSON || blankPiskelJSON(init.canvasSide || 64);

    loadDocument(json, function () {
      installButton(init.saveButtonLabel || "Save");
    });
  });
})();
