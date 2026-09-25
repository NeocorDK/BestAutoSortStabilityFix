# BestAutoSort — Stability Fix (unofficial)

Unofficial fork of **[BestAutoSort](https://github.com/maks2204/BestAutoSort) 0.4.0 by maks2204** with crash, freeze and item-loss fixes.
All features and settings are the original's. Only bugs were fixed. MIT license, original copyright kept (see `LICENSE`).

## Install

- **Remove or disable the original BestAutoSort.** This fork uses the same plugin GUID and config file (`dev.maks2204.bestautosort.cfg`), so both cannot load at once.
- Every player and the dedicated server should run this same build.
- The network protocol is unchanged (still reports 0.4.0), so it stays compatible during rollout. Mixed setups keep the original's bugs on the unpatched side.

## Fixed

- **Stack froze the game with a full inventory**, and on dedicated servers the player was kicked. Batches over 4 items reported per chunk, and the quick-stack cascade treated each chunk as the whole batch. Every chunk re-sent almost the whole inventory to the next chest, and that split again, giving chunks^chests transactions in one frame.
- Quick-stack cascade no longer grabs hotbar, equipped, locked or restock-target stacks. There is a runaway guard of 128 submits per session.
- Items vanished when the inventory was full. Leftovers from compensation and drag now drop at your feet.
- Production loans (fuel and ore from chests) no longer overlap slot (0,0) in a full inventory, and loans return to owned chests again.
- Requests to unavailable chests always complete, so restock, quick-stack and return chains no longer stall and compensation items are no longer voided.
- Chest ownership handover no longer saves the chest once per item. Commits no longer trigger a full vanilla reload, and the last transaction survives a handover.
- Access or version rejects are no longer reported as "applied but items unrecoverable".
- Fixed "collection modified" exceptions in the transaction pump, the `SetInUse` error on a dedicated server acting as chest manager, and stray gamepad hotkeys on the added buttons.
- Freeze diagnostics: `LogOutput.log` warns when a frame step, a chest transaction or a chest load takes more than 100 ms.

See `CHANGELOG.md` for details.
