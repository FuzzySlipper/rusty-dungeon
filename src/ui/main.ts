/**
 * Rusty Dungeon's DOM companion: the HUD of a Delver-style descent — vitals,
 * messages, the flat hotbar, the explored map window, and the level-up choice.
 *
 * It owns no state, evaluates no rules, and starts no loop or timer. Every
 * pixel is rendered from the last admitted `delve.ui.snapshot.v1` projection,
 * and every control claims one declared intent from the staged manifest, so a
 * control cannot send something nothing handles. Game rendering, the canvas,
 * and input delivery stay with the Engine; run state stays with C#.
 */

import type { RustyApplicationUiContext } from '@rusty-engine/product-ui';

interface HudSlot {
  readonly index: number;
  readonly itemId: string | null;
  readonly name: string | null;
  readonly kind: string;
  readonly count: number;
  readonly wielded: boolean;
  /** A wand's charges left; −1 (or absent in older projections) for anything else. */
  readonly charges?: number;
}

interface CampFacts {
  readonly gold: number;
  readonly wins: number;
  readonly deaths: number;
  readonly hotbarSize: number;
  readonly backpackSize: number;
  readonly offers: readonly { readonly label: string; readonly cost: number; readonly affordable: boolean }[];
  readonly cursor: number;
  readonly stash: readonly string[];
  readonly message: string;
}

interface MinimapFacts {
  readonly size: number;
  readonly cells: string;
  readonly playerX: number;
  readonly playerY: number;
  readonly heading: number;
}

interface HudSnapshot {
  readonly phase: string;
  readonly runPhase?: string;
  readonly runIndex?: number;
  readonly section?: string;
  readonly dungeonLevel?: number;
  readonly hp?: number;
  readonly maxHp?: number;
  readonly playerLevel?: number;
  readonly experience?: number;
  readonly experienceToNext?: number;
  readonly gold?: number;
  readonly keys?: number;
  readonly holdingOrb?: boolean;
  readonly escapePressure?: number;
  readonly usePrompt?: string;
  readonly inventoryOpen?: boolean;
  readonly mapOpen?: boolean;
  readonly messages?: readonly string[];
  readonly hotbar?: readonly HudSlot[];
  readonly stats?: Record<string, number>;
  readonly levelUp?: { readonly offers: readonly string[]; readonly cursor: number };
  readonly camp?: CampFacts;
  readonly minimap?: MinimapFacts;
}

const CONTRACT = 'delve.ui.snapshot.v1';

/** Declared direct intents; the csproj declares the same names. */
const INTENTS = {
  confirm: 'menu.confirm',
  cancel: 'menu.cancel',
  up: 'menu.up',
  down: 'menu.down',
} as const;

/** Claim one declared digital intent as a press. */
function claim(context: RustyApplicationUiContext | undefined, intent: string): void {
  context?.intents?.claim(intent, { kind: "digital", active: true });
}

function el<K extends keyof HTMLElementTagNameMap>(
  doc: Document,
  tag: K,
  className?: string,
  text?: string,
): HTMLElementTagNameMap[K] {
  const node = doc.createElement(tag);
  if (className !== undefined) {
    node.className = className;
  }
  if (text !== undefined) {
    node.textContent = text;
  }
  return node;
}

/**
 * Render one snapshot into the panel. Exported for DOM tests; the mount below
 * wires it to the projection stream.
 */
export function renderHud(doc: Document, root: HTMLElement, snapshot: HudSnapshot | null): void {
  root.replaceChildren();
  if (snapshot === null) {
    root.append(el(doc, 'p', 'delve-status', 'Waiting for the first projection.'));
    return;
  }

  const phase = el(doc, 'section', 'delve-phase');
  phase.append(el(doc, 'h1', undefined, 'Rusty Dungeon'));
  phase.append(el(doc, 'p', 'delve-phase-line', describePhase(snapshot)));
  root.append(phase);

  if (snapshot.hp !== undefined && snapshot.maxHp !== undefined) {
    const health = el(doc, 'section', 'delve-health');
    const low = snapshot.hp <= snapshot.maxHp * 0.2;
    health.append(el(doc, 'output', low ? 'delve-hp delve-hp-low' : 'delve-hp', `${snapshot.hp} / ${snapshot.maxHp}`));
    root.append(health);
  }

  if (snapshot.messages !== undefined && snapshot.messages.length > 0) {
    const messages = el(doc, 'section', 'delve-messages');
    for (const line of snapshot.messages) {
      messages.append(el(doc, 'p', 'delve-message', line));
    }
    root.append(messages);
  }

  if (snapshot.usePrompt !== undefined && snapshot.usePrompt.length > 0) {
    root.append(el(doc, 'p', 'delve-use-prompt', snapshot.usePrompt));
  }

  if (snapshot.hotbar !== undefined) {
    const hotbar = el(doc, 'section', 'delve-hotbar');
    hotbar.append(el(doc, 'h2', undefined, 'Hotbar'));
    const list = el(doc, 'ul', 'delve-hotbar-slots');
    for (const slot of snapshot.hotbar) {
      const item = el(doc, 'li', slot.wielded ? 'delve-slot delve-slot-wielded' : 'delve-slot');
      item.dataset.slot = String(slot.index + 1);
      item.append(el(doc, 'span', 'delve-slot-key', String(slot.index + 1)));
      item.append(el(doc, 'span', 'delve-slot-name', slot.name ?? '—'));
      if (slot.count > 1) {
        item.append(el(doc, 'span', 'delve-slot-count', `×${slot.count}`));
      }
      if (slot.charges !== undefined && slot.charges >= 0) {
        item.append(el(doc, 'span', 'delve-slot-charges', `${slot.charges} charges`));
      }
      list.append(item);
    }
    hotbar.append(list);
    root.append(hotbar);
  }

  if (snapshot.stats !== undefined) {
    const stats = el(doc, 'section', 'delve-stats');
    const parts = Object.entries(snapshot.stats).map(([name, value]) => `${name} ${value}`);
    stats.append(el(doc, 'p', undefined, `Level ${snapshot.playerLevel ?? 1} · xp ${snapshot.experience ?? 0}/${snapshot.experienceToNext ?? 0}`));
    stats.append(el(doc, 'p', undefined, parts.join(' · ')));
    stats.append(el(doc, 'p', undefined, `Gold ${snapshot.gold ?? 0} · Keys ${snapshot.keys ?? 0}`));
    root.append(stats);
  }

  if (snapshot.holdingOrb === true) {
    root.append(el(doc, 'p', 'delve-escape', 'Holding the orb — escape!'));
  }

  if (snapshot.minimap !== undefined) {
    root.append(renderMinimap(doc, snapshot.minimap));
  }

  if (snapshot.camp !== undefined) {
    root.append(renderCamp(doc, snapshot.camp));
  }

  if (snapshot.levelUp !== undefined && snapshot.levelUp.offers.length > 0) {
    const chooser = el(doc, 'section', 'delve-levelup');
    chooser.append(el(doc, 'h2', undefined, 'Choose your fate'));
    const list = el(doc, 'ul', 'delve-offers');
    snapshot.levelUp.offers.forEach((offer, index) => {
      const item = el(doc, 'li', index === snapshot.levelUp?.cursor ? 'delve-offer delve-offer-cursor' : 'delve-offer', offer);
      list.append(item);
    });
    chooser.append(list);
    root.append(chooser);
  }
}

function renderCamp(doc: Document, camp: CampFacts): HTMLElement {
  const section = el(doc, 'section', 'delve-camp');
  section.append(el(doc, 'h2', undefined, 'Camp'));
  section.append(el(doc, 'p', 'delve-camp-purse', `${camp.gold} gold · ${camp.wins} escapes · ${camp.deaths} deaths`));
  section.append(el(doc, 'p', 'delve-camp-slots', `Belt ${camp.hotbarSize} slots · pack ${camp.backpackSize} slots`));
  const list = el(doc, 'ul', 'delve-camp-offers');
  camp.offers.forEach((offer, index) => {
    const classes = ['delve-camp-offer'];
    if (index === camp.cursor) classes.push('delve-offer-cursor');
    if (!offer.affordable) classes.push('delve-camp-offer-dear');
    const label = offer.cost > 0 ? `${offer.label} — ${offer.cost} gold` : offer.label;
    list.append(el(doc, 'li', classes.join(' '), label));
  });
  section.append(list);
  if (camp.stash.length > 0) {
    section.append(el(doc, 'p', 'delve-camp-stash', `Going down with you: ${camp.stash.join(', ')}`));
  }
  if (camp.message.length > 0) {
    section.append(el(doc, 'p', 'delve-camp-message', camp.message));
  }
  return section;
}

function describePhase(snapshot: HudSnapshot): string {
  switch (snapshot.phase) {
    case 'title':
      return 'Press Enter to go to camp.';
    case 'camp':
      return 'Camp: W/S to choose, Enter to buy or descend, Esc for the title.';
    case 'dead':
      return 'You have died. Press Enter to return to the title.';
    case 'won':
      return 'You escaped with the orb. Press Enter to return to the title.';
    case 'levelup':
      return 'Choose a stat with the arrow keys, Enter to take it.';
    default: {
      const section = snapshot.section ?? 'The depths';
      return `${section} · dungeon level ${snapshot.dungeonLevel ?? 1}`;
    }
  }
}

function renderMinimap(doc: Document, minimap: MinimapFacts): HTMLElement {
  const section = el(doc, 'section', 'delve-minimap');
  section.append(el(doc, 'h2', undefined, 'Map'));
  const grid = el(doc, 'div', 'delve-minimap-grid');
  grid.setAttribute('role', 'img');
  grid.setAttribute('aria-label', 'Explored map around the player');
  for (let row = 0; row < minimap.size; row += 1) {
    const line = el(doc, 'div', 'delve-minimap-row');
    line.textContent = minimap.cells.slice(row * minimap.size, (row + 1) * minimap.size);
    if (row === Math.floor(minimap.size / 2)) {
      line.className = 'delve-minimap-row delve-minimap-player';
    }
    grid.append(line);
  }
  section.append(grid);
  return section;
}

/**
 * Mount the HUD. Renders only from admitted projections; menu buttons claim
 * declared intents; disposal removes every listener and node.
 */
export function mountProductUi(root: HTMLElement, context?: RustyApplicationUiContext): { dispose(): void } {
  const doc = root.ownerDocument;
  const panel = el(doc, 'aside', 'delve-hud');
  panel.setAttribute('aria-label', 'Rusty Dungeon status');

  const body = el(doc, 'div', 'delve-hud-body');
  panel.append(body);

  const controls = el(doc, 'nav', 'delve-controls');
  for (const [label, intent] of [
    ['Confirm', INTENTS.confirm],
    ['Cancel', INTENTS.cancel],
    ['Up', INTENTS.up],
    ['Down', INTENTS.down],
  ] as const) {
    const button = el(doc, 'button', 'delve-control', label);
    button.type = 'button';
    button.dataset.intent = intent;
    const onPress = () => claim(context, intent);
    button.addEventListener('click', onPress);
    controls.append(button);
  }
  panel.append(controls);
  root.append(panel);

  renderHud(doc, body, null);

  const unsubscribe = context?.projection?.subscribe((envelope) => {
    if (envelope === null || envelope.contract !== CONTRACT) {
      return;
    }
    // The contract identity is the gate: an admitted delve.ui.snapshot.v1
    // value is the C# HUD projection's shape.
    renderHud(doc, body, envelope.value as unknown as HudSnapshot);
  });

  return Object.freeze({
    dispose: () => {
      unsubscribe?.();
      panel.remove();
    },
  });
}
