using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;
using TMPro;
using SuperFantasyKingdom;

namespace CaravanCommissions
{
    internal sealed class PoolShortcutHint
    {
        private readonly RectTransform row;
        private readonly TMP_Text keyName;
        private readonly Image glyph;
        private InputControl displayedControl;
        private int displayedBinding = -1;
        private string displayedPath;

        internal PoolShortcutHint(RectTransform row, TMP_Text keyName, Image glyph)
        {
            this.row = row;
            this.keyName = keyName;
            this.glyph = glyph;
        }

        internal bool Refresh(InputAction action, PlayerInput player, GamepadManager pad)
        {
            var scheme = player?.currentControlScheme;
            if (action == null || !action.enabled || string.IsNullOrEmpty(scheme)) return Hide();

            foreach (var control in action.controls)
            {
                if (!player.devices.Contains(control.device)) continue;
                int index = action.GetBindingIndexForControl(control);
                if (index < 0 || !SupportsScheme(action.bindings[index].groups, scheme)) continue;
                string path = action.bindings[index].effectivePath;
                if (displayedControl != control || displayedBinding != index || displayedPath != path)
                {
                    displayedControl = control;
                    displayedBinding = index;
                    displayedPath = path;
                    // The game's glyph table covers Xbox and PlayStation. Other layouts
                    // display the actual binding name supplied by their input device.
                    bool nativeGlyph = control.device is DualShockGamepad ||
                        (control.device is Gamepad && (control.device.layout == "Gamepad" ||
                            InputSystem.IsFirstLayoutBasedOnSecond(control.device.layout, "XInputController")));
                    glyph.sprite = nativeGlyph ? pad.GetButtonPrompt(path) : null;
                    glyph.enabled = glyph.sprite != null;
                    keyName.text = glyph.enabled ? "" : $"[{action.GetBindingDisplayString(index)}]";
                }
                row.gameObject.SetActive(true);
                return true;
            }
            return Hide();
        }

        private bool Hide()
        {
            row.gameObject.SetActive(false);
            displayedControl = null;
            return false;
        }

        private static bool SupportsScheme(string groups, string scheme)
        {
            if (string.IsNullOrEmpty(groups)) return true;
            foreach (var group in groups.Split(';'))
                if (string.Equals(group, scheme, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
