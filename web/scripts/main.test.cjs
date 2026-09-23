const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { test } = require('node:test');
const { runInNewContext } = require('node:vm');
const source = readFileSync(join(__dirname, 'main.js'), 'utf8');

function boot({ reduced = false, userAgent = '', touch = 0, withPet = true } = {}) {
  const handlers = {};
  const pet = { dataset: { animation: 'pet.webp', still: 'pet.png' } };
  const toggle = { hidden: true, addEventListener: (name, handler) => { handlers[name] = handler; } };
  const query = { matches: reduced, addEventListener: (_, handler) => { handlers.motionChange = handler; } };
  const context = {
    document: {
      querySelector: (selector) => withPet ? ({ '[data-animation]': pet, '[data-motion-toggle]': toggle }[selector] || null) : null,
      querySelectorAll: () => [],
    },
    navigator: { userAgent, maxTouchPoints: touch },
    window: { matchMedia: () => query },
  };
  runInNewContext(source, context);
  return { pet, toggle, handlers, context };
}

test('normal mode starts animation and can stop / restart it', () => {
  const { pet, toggle, handlers } = boot();
  assert.equal(pet.src, 'pet.webp');
  assert.equal(toggle.hidden, false);
  handlers.click();
  assert.equal(pet.src, 'pet.png');
  assert.equal(toggle.textContent, '움직임 재생');
  handlers.click();
  assert.equal(pet.src, 'pet.webp');
});

test('reduced-motion starts still; an explicit click may play', () => {
  const { pet, handlers } = boot({ reduced: true });
  assert.equal(pet.src, 'pet.png');
  handlers.click();
  assert.equal(pet.src, 'pet.webp');
  handlers.motionChange({ matches: true });
  assert.equal(pet.src, 'pet.png');
});

test('shared script is safe on pages without media or forms', () => {
  assert.doesNotThrow(() => boot({ withPet: false }));
});

test('OS hint does not mistake iPad desktop mode for a Mac', () => {
  assert.equal(boot({ userAgent: 'Macintosh', touch: 5 }).context.detectOs(), null);
  assert.equal(boot({ userAgent: 'Macintosh', touch: 0 }).context.detectOs(), 'mac');
  assert.equal(boot({ userAgent: 'Windows NT 10.0' }).context.detectOs(), 'windows');
  assert.equal(boot({ userAgent: 'Linux' }).context.detectOs(), null);
});
