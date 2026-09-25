# Changelog

## 0.4.0-fix (unofficial stability patch, wire-compatible with 0.4.0)

- **Stack freeze fixed.** Batches over 4 items now report once, aligned to the whole batch. Per-chunk completions were read by the quick-stack cascade as whole-batch results: every chunk re-sent nearly the whole inventory to the next chest, which split again (chunks^chests transactions; synchronous in one frame for owned chests). Full inventories hit it hardest.
- Quick-stack cascade remainder respects hotbar/equipped/locked/restock filters; runaway guard (128 submits per session).
- Restock is not started again while its previous chain is still running.
- Pending-tx pump no longer mutates dictionaries under enumeration (InvalidOperationException out of `Update`); a chest destroyed mid-flight still completes its callback.

- Owner-side chest loads (takeover, structural acquire, viewer refresh) run with `Container.m_loading` set: no more N saves / N ZDO revisions per load.
- Commit writes the idempotency ring after recording the tx (last tx survives handoff) and resyncs `m_lastRevision` (no vanilla full reload after every commit).
- Access/version rejects no longer surface as "applied but items unrecoverable".
- `Submit` always answers its callback: restock / quick-stack / return chains no longer stall, compensation leftovers are no longer voided.
- Full player inventory: compensation and drag leftovers are dropped at the player's feet instead of silently vanishing (`Humanoid.DropItem` on a non-inventory item drops nothing).
- Production loans take a free cell only (no forced overlap at slot 0,0); loans return to owned chests again.
- Dedicated server as chest manager: no `SetInUse` NRE.
- Owner take results hand out clones, not the cached `ItemData`.
- Cloned toolbar buttons drop the template's gamepad hotkey.
- Freeze diagnostics: warnings for frame steps, tx applies and chest loads over 100 ms.

## 0.4.0

- Legacy chests no longer shrink a larger runtime inventory applied by other mods (final = max(current, template), issue #8).
- Eating food from a shared chest as non-owner no longer pulls the whole stack into inventory (take 1 unit, issue #6).

## 0.3.2

- Staged-stock return no longer breaks crafting (craft stages untracked) and no longer loops on upgrades (recent-upgrade keep window).

## 0.3.1

- Deferred building pipelines the next set (no more multi-click starvation); unused staged stock returns to chests on selection change, hammer put-away, or late landing.

## 0.3.0

- Absolute tx idempotency (no re-apply after handoff), retry-or-loud client, no-void compensation.
- Owned-chest persistence everywhere (restock, loans, quick-stack into open, trash, sort, replacement).
- Deferred builds and upgrades consume chest stock; quest items never leak via TakeAll/drop/right-click.
- Shared-mode deny for direct TakeAll RPC; lease-safe chest open; drag-take lands in the drop cell.

## 0.2.0

- Feeder flights target the animal, not the player.
- Upgrade button stages chest stock and consumes owned-chest resources; replacement chest contents persisted (no more ghost items).
- Chest buttons narrowed to Trash/Stack width; Sort Chest renamed to Sort.

## 0.1.10

- BepInExPack 5.4.2350, simultaneous-access highlight, protocol docs.

## 0.1.9

- Simultaneous chest access highlight, protocol docs with diagrams, packaging fixes.

## 0.1.8

- Transport-safe batching, feeder on transactions, deferred placement, conservation gates.

## 0.1.7

- Pull-on-press crafting (no browse-time floods), chest-inclusive craft/build display, DoCrafting conservation gate.
- In-flight Add deduplication, drag/grid-inventory fixes, monotonic viewer, GearSlots tag handling.
- Verified against **Valheim 1.0.12**.

## 0.1.6

- Wire protocol v2 (client-stamped actor position), build commit hash baked into the DLL.

## 0.1.5

- Quick-stack outcome visibility and skip diagnostics, build tag in startup log.

## 0.1.4

- Owner quick-stack through the tx queue (broadcasts for all), quick-stack entry logging.

## 0.1.3

- Flight visuals broadcast (TxFlights) + manager-side play for remote batches.

## 0.1.2

- Fix response body framing (nested package was read flat: Adds silently applied 0, Takes crashed) and short presence/query bodies (heartbeats died, lid flapped).

## 0.1.1

- Wire protocol fix (ProtoVersion header) + version gate: mixed 0.1.0/0.1.1 peers are rejected loudly instead of silent garbage.

## 0.1.0

- Rebrand to BestAutoSort (`dev.maks2204.bestautosort`): new GUID, name, version, own plugin folder.
- Fresh config `dev.maks2204.bestautosort.cfg`, no legacy imports.
- Console command renamed to `bestautosort`.
- Full storage feature set (quick-stack, restock, rules, crafting/fuel from chests,
  autofeed, shared chests, upgrades, sort, trash), no ModCore dependency.
- Own save-data keys, no compatibility with other storage mods by design.
