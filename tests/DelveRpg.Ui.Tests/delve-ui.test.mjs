import { test } from 'node:test';
import assert from 'node:assert/strict';
import { JSDOM } from 'jsdom';
import { mountProductUi, renderHud } from '../../src/ui/generated/main.js';

/**
 * The companion's contract: one projection stream in (`delve.ui.snapshot.v1`),
 * declared intents out. Values are rendered only from the last admitted
 * projection; nothing here owns state or starts a timer.
 */

function setup() {
  const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>');
  return { dom, root: dom.window.document.getElementById('root') };
}

function context() {
  const listeners = [];
  const claimed = [];
  return {
    listeners,
    claimed,
    projection: {
      subscribe: (fn) => {
        listeners.push(fn);
        return () => {
          listeners.splice(listeners.indexOf(fn), 1);
        };
      },
    },
    intents: { claim: (intent, value) => claimed.push({ intent, value }) },
  };
}

const snapshot = {
  phase: 'run',
  runPhase: 'Playing',
  runIndex: 1,
  section: 'The Sewers',
  dungeonLevel: 2,
  hp: 7,
  maxHp: 16,
  playerLevel: 2,
  experience: 3,
  experienceToNext: 32,
  gold: 40,
  keys: 1,
  holdingOrb: false,
  escapePressure: 0,
  usePrompt: 'E — open door',
  inventoryOpen: false,
  mapOpen: false,
  messages: ['You pick up the sword.'],
  hotbar: [
    { index: 0, itemId: 'delve.item.short-sword', name: 'short sword', kind: 'Weapon', count: 1, wielded: true },
    { index: 1, itemId: 'delve.item.potion-of-healing', name: 'potion of healing', kind: 'Potion', count: 3, wielded: false },
    { index: 2, itemId: 'delve.item.wand-of-sparks', name: 'wand of sparks', kind: 'Wand', count: 1, wielded: false, charges: 12 },
    { index: 3, itemId: null, name: null, kind: 'Empty', count: 0, wielded: false },
    { index: 4, itemId: null, name: null, kind: 'Empty', count: 0, wielded: false },
    { index: 5, itemId: null, name: null, kind: 'Empty', count: 0, wielded: false },
  ],
  stats: { attack: 4, defense: 2, dexterity: 4, speed: 4, magic: 2, endurance: 6 },
  levelUp: { offers: [], cursor: 0 },
  minimap: {
    size: 3,
    cells: ' #.>   ..@',
    playerX: 10,
    playerY: 11,
    heading: 90,
  },
};

test('renderHud shows vitals, messages, and the use prompt', () => {
  const { dom, root } = setup();
  renderHud(dom.window.document, root, snapshot);
  assert.match(root.textContent, /7 \/ 16/);
  assert.match(root.textContent, /The Sewers · dungeon level 2/);
  assert.match(root.textContent, /You pick up the sword\./);
  assert.match(root.textContent, /E — open door/);
});

test('renderHud shows the flat hotbar with stack counts and the wielded slot', () => {
  const { dom, root } = setup();
  renderHud(dom.window.document, root, snapshot);
  const slots = root.querySelectorAll('.delve-slot');
  assert.equal(slots.length, 6);
  assert.match(slots[0].textContent, /short sword/);
  assert.ok(slots[0].className.includes('delve-slot-wielded'));
  assert.match(slots[1].textContent, /×3/);
  assert.match(slots[2].textContent, /12 charges/);
  assert.match(slots[3].textContent, /—/);
  assert.doesNotMatch(slots[0].textContent, /charges/);
});

test('renderHud shows the explored map window', () => {
  const { dom, root } = setup();
  renderHud(dom.window.document, root, snapshot);
  const rows = root.querySelectorAll('.delve-minimap-row');
  assert.equal(rows.length, 3);
  assert.equal(rows[0].textContent, ' #.');
  assert.ok(rows[1].className.includes('delve-minimap-player'));
});

test('renderHud shows the level-up chooser with its cursor', () => {
  const { dom, root } = setup();
  renderHud(dom.window.document, root, {
    ...snapshot,
    phase: 'levelup',
    levelUp: { offers: ['attack', 'defense', 'endurance'], cursor: 1 },
  });
  const offers = root.querySelectorAll('.delve-offer');
  assert.deepEqual([...offers].map((node) => node.textContent), ['attack', 'defense', 'endurance']);
  assert.ok(offers[1].className.includes('delve-offer-cursor'));
});

test('renderHud marks low health distinctly', () => {
  const { dom, root } = setup();
  renderHud(dom.window.document, root, { ...snapshot, hp: 2, maxHp: 16 });
  assert.ok(root.querySelector('.delve-hp-low'));
});

test('mountProductUi renders only the subscribed contract', () => {
  const { dom, root } = setup();
  const ctx = context();
  const mount = mountProductUi(root, ctx);

  const deliver = (envelope) => ctx.listeners.forEach((fn) => fn(envelope));
  deliver({ contract: 'other.contract.v1', value: snapshot });
  assert.match(root.textContent, /Waiting for the first projection/);

  deliver({ contract: 'delve.ui.snapshot.v1', value: snapshot });
  assert.match(root.textContent, /The Sewers/);
  mount.dispose();
});

test('mountProductUi claims declared intents from its controls', () => {
  const { dom, root } = setup();
  const ctx = context();
  const mount = mountProductUi(root, ctx);

  for (const button of root.querySelectorAll('.delve-control')) {
    button.click();
  }

  assert.deepEqual(
    ctx.claimed.map((entry) => entry.intent),
    ['menu.confirm', 'menu.cancel', 'menu.up', 'menu.down'],
  );
  assert.ok(ctx.claimed.every((entry) => entry.value.kind === 'digital' && entry.value.active === true));
  mount.dispose();
});

test('dispose removes the panel and unsubscribes', () => {
  const { dom, root } = setup();
  const ctx = context();
  const mount = mountProductUi(root, ctx);
  assert.equal(ctx.listeners.length, 1);

  mount.dispose();
  assert.equal(ctx.listeners.length, 0);
  assert.equal(root.querySelector('.delve-hud'), null);
});

test('renderHud shows the camp menu with the purse, the cursor and dear offers', () => {
  const { dom, root } = setup();
  renderHud(dom.window.document, root, {
    phase: 'camp',
    camp: {
      gold: 45, wins: 1, deaths: 3, hotbarSize: 6, backpackSize: 19,
      offers: [
        { label: 'Descend into the dungeon', cost: 0, affordable: true },
        { label: 'Soulbound bag expansion (+1 backpack slot)', cost: 52, affordable: false },
        { label: 'hunting bow', cost: 25, affordable: true },
      ],
      cursor: 2,
      stash: ['short sword'],
      message: 'You buy the short sword; it will go down with you.',
    },
  });
  assert.match(root.textContent, /45 gold/);
  const offers = root.querySelectorAll('.delve-camp-offer');
  assert.equal(offers.length, 3);
  assert.ok(offers[2].className.includes('delve-offer-cursor'));
  assert.ok(offers[1].className.includes('delve-camp-offer-dear'));
  assert.match(offers[1].textContent, /52 gold/);
  assert.match(root.textContent, /Going down with you: short sword/);
  assert.match(root.textContent, /Enter to buy or descend/);
});

