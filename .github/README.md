# 🌸 Animator Extensions

Forked from https://github.com/lukis101/VRCUnityStuffs/tree/master/Scripts/Editor

## [Download](https://github.com/rrazgriz/AnimatorExtensions/releases)

⚠ This is experimental! It's doing a lot, so it might break things. Hopefully not, but please let me know if it does 💕

Some [Harmony](https://github.com/pardeike/Harmony)-based patches to Unity's Animator window.

This tool packages a re-namespaced version of Harmony (compiled for .NET 4.7.1) to avoid causing issues if you already have a Harmony dll in the project. [1]

Tested on Unity versions:

- `2019.4.31f1`

## Usage

Either:

- [Download latest release `.unitypackage`](https://github.com/rrazgriz/AnimatorExtensions/releases) and import to project
- Clone Repo, add `Razgriz` folder to Assets folder of project

Configure at `Tools -> AnimatorExtensions`. Settings are saved in EditorPrefs, so they persist across any projects that have AnimatorExtensions installed.

## Images

![Animator Extensions Features](.img/img-AnimatorExtensions-Features.png)

## Features

### New Options

- Change default state Write Defaults setting
- Change default Layer Weight to 1
- Change default Transition Exit/Transition time to 0
- Show extra labels on animation states
  - Animation clip/BlendTree name
  - `B` if a state has State Behaviors
  - `WD` if a state has Write Defaults enabled
  - `M` if a state has a motion time parameter
  - `S` if a state has a speed parameter
  - Icons for Blendtree/Loop Time
  - Warning icons For empty states/animations
- Hold Alt to view all labels regardless of setting
- Animation Window: Show actual property name instead of "Display Name" in animation hierarchy
- Animation Window: Show full path of keyframes
- Animation Window: Reduce/Disable Indentation

### Tweaks/Fixes

- Layer copy-pasting and duplication (including cross-controller) via context menu or keyboard shortcuts
- F2 keyboard shortcut to rename selected layer
- Instead of the annoying list scrollbar reset, get new or edited layer in view
- Similarly scroll to bottom when adding a new parameter
- Prevent transition condition mode/function resetting when swapping parameter
- Highlight/select animator controller by single/double-clicking its path in bottom bar
- Disable undo of "Paste Sub-Sate Machine" action as it leaves dangling sub-assets.  
  - Manually delete pasted layers or sub-state machines to correctly dispose of majority _(but still not all)_ of sub-assets!

## Known Issues

- Icons/Indicators are not super visible in Unity Light Mode
- `Unsupported.PasteToStateMachineFromPasteboard` copies some parameters, but does not copy their default values
  - It also does not have proper undo handling causing dangling sub-assets left in the controller
- State node motion label overlaps progress bar in "Live Link" mode
- Animation Window "Show Actual Names" requires redraw (select something different) to disable

## TODO

- Animation: Option to rename property names
- Animator: Recolor States
- Animator: Drag animation onto State to change
- Animator: Option to disable loop time
- Project Window: show multiple columns of data about assets? asset type, filetype

Maybe:

- Animation: Implement needle tools drag-to-retarget on Animation paths
- Animator: Add undo callback handler to delete sub-state machines properly
- Animator: Clean up layer copy/paste (don't take up clipboard)

## Credits

- [DJ Lukis.LT](https://github.com/lukis101/) for the original utility script (MIT-licensed!)
- [Andreas Pardeike](https://github.com/pardeike/Harmony) for the fantastic Harmony library (MIT-licensed!)
- [Pumkin](https://github.com/rurre/) for lots of help with figuring out Harmony (and Reflection in general)

---

[1] The release Harmony DLL and this re-namespaced one can coexist just fine in the same project. If you would like to use only the standard Harmony release, just remove (or don't import) `Razgriz.AnimatorExtensions.0Harmony.dll` and edit `AnimatorExtensions.cs` to have `using HarmonyLib;` instead of `using Razgriz.AnimatorExtensions.HarmonyLib;`.
