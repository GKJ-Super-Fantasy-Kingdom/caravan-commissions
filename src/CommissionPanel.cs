using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CaravanCommissions
{
    internal sealed class CommissionPanel : MonoBehaviour
    {
        private const int Price = 3;
        private UIOverlayMerchant merchant;
        private Plugin.Visit visit;
        private List<Pool> pools;
        private Item[] today;
        private ResourceData material;
        private TMP_Text sample;
        private CanvasGroup native;
        private bool oldInteractable, oldRaycasts, addedGroup;
        internal static int LastBackFrame = -1;
        private RectTransform page;
        private readonly List<Button> buttons = new List<Button>();
        private int index, enteredFrame;
        private bool reviewing;
        private TMP_Text acceptPrompt, backPrompt;
        private Image acceptIcon, backIcon;
        private string activeScheme;
        private static readonly Color Ink = new Color(.075f, .105f, .13f);
        private static readonly Color Paper = new Color(.96f, .92f, .78f);

        internal static void Open(UIOverlayMerchant merchant, Plugin.Visit visit)
        {
            var root = new GameObject("Caravan Commissions", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var panel = root.AddComponent<CommissionPanel>();
            try
            {
                Plugin.Panel = panel;
                panel.Initialize(merchant, visit);
            }
            catch
            {
                panel.Dismiss();
                throw;
            }
        }

        private void Initialize(UIOverlayMerchant screen, Plugin.Visit current)
        {
            merchant = screen;
            visit = current;
            pools = Pool.Eligible(visit.Guest);
            var ids = (List<string>)AccessTools.Field(typeof(GuestCaravan), "m_Chosen").GetValue(visit.Guest);
            today = ids.Select(id => AddressablesManager.Instance.GetItem(id)).Where(x => x != null).ToArray();
            material = new[] { ResourceType.Wood, ResourceType.Stone, ResourceType.Board }
                .Select(type => RaceManager.Instance.GetResourceData(type))
                .FirstOrDefault(data => data != null && data.type != ResourceType.None && data.buildingToTakeFrom != SuperFantasyKingdom.Buildings.BuildingType.None);
            sample = merchant.transform.Find("HeaderContainer/Header").GetComponent<TMP_Text>();
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            native = merchant.GetComponent<CanvasGroup>();
            addedGroup = native == null;
            if (addedGroup) native = merchant.gameObject.AddComponent<CanvasGroup>();
            oldInteractable = native.interactable;
            oldRaycasts = native.blocksRaycasts;
            native.interactable = native.blocksRaycasts = false;
            Render(0);
        }

        private void LateUpdate()
        {
            if (merchant == null || !merchant.isActiveAndEnabled || !visit.Available) { Dismiss(); return; }
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform) &&
                GamepadManager.Instance.GetGameObjectToSelect() != selected)
                GamepadManager.Instance.SetGameObjectToSelect(selected, true);
            string scheme = GamepadManager.Instance.GetCurrentControlScheme();
            if (activeScheme != scheme) RefreshPrompts(scheme);
        }

        private static RectTransform Box(string name, Transform parent, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private TMP_Text Label(string name, RectTransform parent, string value, float left, float bottom,
            float right, float top, int maxSize, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var label = Box(name, parent, left, bottom, right, top).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = sample.font;
            label.spriteAsset = sample.spriteAsset;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = maxSize;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.color = Paper;
            label.text = value;
            return label;
        }

        private void Column(string title, Item[] items, float left, float right)
        {
            Label("Pool", page, title, left, .67f, right, .76f, 27);
            for (int i = 0; i < items.Length; i++)
            {
                float top = .66f - i * .123f;
                var item = items[i];
                var icon = Box("Item icon", page, left, top - .09f, left + .046f, top).gameObject.AddComponent<Image>();
                icon.sprite = item.GetIcon();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Label("Item", page, "<b>" + item.GetTitle() + "</b>\n" + item.GetDescription(),
                    left + .055f, top - .118f, right, top, 21);
            }
        }

        private void Render(int focus)
        {
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            buttons.Clear();
            page = Box("Order desk", transform, 0, 0, 1, 1);
            page.gameObject.AddComponent<Image>().color = Ink;
            enteredFrame = Time.frameCount;
            var pool = pools.Count == 0 ? null : pools[index];
            int owned = Spendable();
            string name = material == null ? "material" : material.GetName();
            string cost = $"Cost: {Price} {name}     Available: {owned}";
            if (owned < Price) cost += $"     Short by {Price - owned}";
            Label("Heading", page, reviewing ? "Confirm commission" : "Caravan commissions", .06f, .82f, .95f, .95f, 42);
            Label("Terms", page, reviewing ? $"Spend {Price} {name} and give up today’s item for {pool.Title}?" :
                "Take one item today, or pay for a choice at the next caravan visit.", .06f, .76f, .96f, .83f, 24);
            Column("Today’s items", today, .06f, .48f);
            Column(pool == null ? "No eligible pool" : pool.Title + " · next visit", pool?.Items ?? Array.Empty<Item>(), .52f, .94f);
            Label("Cost", page, cost, .06f, .217f, .94f, .29f, 25);
            if (reviewing)
            {
                AddButton("Back to pool", .16f, .47f, () => { reviewing = false; Render(2); });
                AddButton("Pay and commission", .53f, .84f, Pay, owned >= Price);
            }
            else
            {
                AddButton("Previous pool", .045f, .208f, () => Cycle(-1), pools.Count > 1);
                AddButton("Next pool", .217f, .38f, () => Cycle(1), pools.Count > 1);
                AddButton("Review order", .389f, .566f, () => { reviewing = true; Render(0); }, pool != null && owned >= Price);
                AddButton("Today’s items", .575f, .766f, Dismiss);
                AddButton("Leave caravan", .775f, .955f, Leave);
            }
            WireNavigation(focus);
            acceptIcon = PromptIcon(.32f);
            backIcon = PromptIcon(.54f);
            acceptPrompt = Label("Select prompt", page, "", .36f, .009f, .53f, .089f, 18);
            backPrompt = Label("Back prompt", page, "", .58f, .009f, .85f, .089f, 18);
            RefreshPrompts(GamepadManager.Instance.GetCurrentControlScheme());
        }

        private Image PromptIcon(float x)
        {
            var image = Box("Button prompt", page, x, .024f, x + .035f, .077f).gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private void RefreshPrompts(string scheme)
        {
            activeScheme = scheme;
            var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            var actions = GamepadManager.Instance.GetPlayerInput()?.actions;
            string accept = Binding(actions?.FindAction("UI/Submit") ?? module?.submit?.action, acceptIcon, scheme);
            string back = Binding(actions?.FindAction("UI/Cancel") ?? module?.cancel?.action, backIcon, scheme);
            acceptPrompt.text = (accept + "  Select").Trim();
            backPrompt.text = (back + "  Back").Trim();
        }

        private static string Binding(InputAction action, Image image, string scheme)
        {
            int binding = action == null || string.IsNullOrEmpty(scheme) ? -1 : action.GetBindingIndex(group: scheme);
            var sprite = binding < 0 ? null : GamepadManager.Instance.GetButtonPrompt(action.bindings[binding].effectivePath);
            image.sprite = sprite;
            image.enabled = sprite != null;
            return binding < 0 ? "" : action.GetBindingDisplayString(binding);
        }

        private void Cycle(int step)
        {
            index = (index + step + pools.Count) % pools.Count;
            Render(step < 0 ? 0 : 1);
        }

        private void AddButton(string text, float left, float right, Action click, bool enabled = true)
        {
            var rect = Box(text, page, left, .101f, right, .197f);
            var face = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.normalColor = new Color(.17f, .26f, .29f);
            colors.selectedColor = colors.highlightedColor = new Color(.42f, .32f, .13f);
            colors.disabledColor = new Color(.18f, .18f, .19f);
            button.colors = colors;
            button.interactable = enabled;
            button.onClick.AddListener(() => { if (Time.frameCount != enteredFrame && visit.Available) click(); });
            rect.gameObject.AddComponent<PanelInput>().Owner = this;
            Label("Label", rect, text, .03f, .04f, .97f, .96f, 22, TextAlignmentOptions.Center);
            buttons.Add(button);
        }

        private void WireNavigation(int focus)
        {
            var enabled = buttons.Where(b => b.interactable).ToList();
            for (int i = 0; i < enabled.Count; i++)
            {
                var prev = enabled[(i + enabled.Count - 1) % enabled.Count];
                var next = enabled[(i + 1) % enabled.Count];
                enabled[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = prev, selectOnUp = prev, selectOnRight = next, selectOnDown = next };
            }
            Focus(buttons[focus].interactable ? buttons[focus].gameObject : enabled[0].gameObject);
        }

        internal static void Focus(GameObject target)
        {
            GamepadManager.Instance.SetGameObjectToSelect(target, true);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != target)
                EventSystem.current.SetSelectedGameObject(target);
        }

        internal void Back()
        {
            LastBackFrame = Time.frameCount;
            if (Time.frameCount == enteredFrame) return;
            if (reviewing) { reviewing = false; Render(2); }
            else Dismiss();
        }

        private void Pay()
        {
            var pool = pools[index];
            if (!visit.Available || !Plugin.Book.Place(visit.Day, visit.Number, pool.Title, pool.Ids, Debit))
            {
                reviewing = false;
                Render(2);
                return;
            }
            Dismiss();
            OverlayManager.Instance.Confirm();
        }

        private int Spendable()
        {
            if (material == null) return 0;
            var building = CityManager.Instance.GetBuilding(material.buildingToTakeFrom);
            if (building == null) return ResourceManager.Instance.GetResourceAvailableAmount(material.type);
            // The storage total includes reserved packets. Only free single units
            // can be taken at this price, without disturbing worker assignments.
            return building.GetStorage().GetAllResourcePackets().Count(packet =>
                packet.GetResourceType() == material.type && !packet.IsReserved() && packet.GetAmount() == 1);
        }

        private bool Debit()
        {
            var resources = ResourceManager.Instance;
            if (Spendable() < Price) return false;
            var building = CityManager.Instance.GetBuilding(material.buildingToTakeFrom);
            if (building == null)
            {
                // Starting stock is debited even though the game returns false.
                resources.SpendResource(material.type, Price);
                return true;
            }
            var units = building.GetStorage().GetAllResourcePackets().Where(packet =>
                packet.GetResourceType() == material.type && !packet.IsReserved() && packet.GetAmount() == 1)
                .Take(Price).ToArray();
            foreach (var unit in units) building.TakeResourcePacket(unit.GetIdentifier());
            resources.ResourceAmountChanged(material.type);
            return true;
        }

        private void Leave() { Dismiss(); OverlayManager.Instance.Cancel(); }

        internal void Dismiss()
        {
            if (Plugin.Panel != this) return;
            Plugin.Panel = null;
            if (native != null)
            {
                native.interactable = oldInteractable;
                native.blocksRaycasts = oldRaycasts;
                if (addedGroup) Destroy(native);
            }
            gameObject.SetActive(false);
            if (merchant != null && merchant.isActiveAndEnabled)
                Focus(merchant.transform.Find("Panel/Buttons/Cancel/Button").gameObject);
            Destroy(gameObject);
        }
    }

    internal sealed class PanelInput : MonoBehaviour, ICancelHandler
    {
        internal CommissionPanel Owner;
        public void OnCancel(BaseEventData e) { if (Owner != null) Owner.Back(); e.Use(); }
    }
}
