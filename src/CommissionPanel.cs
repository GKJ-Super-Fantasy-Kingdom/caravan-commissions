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
        private const int Price = CommissionPayment.Price;
        private string[] expectedOrder;
        private bool Exchange => expectedOrder != null;
        private static readonly AccessTools.FieldRef<UICardItem, string> CardId =
            AccessTools.FieldRefAccess<UICardItem, string>("m_ItemIdentifier");
        private string[] todayPrices;
        private SuperFantasyKingdom.Buildings.BuildingType source;
        private UIOverlayMerchant merchant;
        private Plugin.Visit visit;
        private List<Pool> pools;
        private Item[] today;
        private ResourceData material;
        private ResourceType materialType;
        private TMP_Text sample;
        private CanvasGroup native;
        private bool oldInteractable, oldRaycasts, addedGroup;
        private readonly List<(CanvasGroup group, float alpha, bool added)> hidden = new List<(CanvasGroup, float, bool)>();
        private Button nativeButton;
        private Sprite cardFrame, cardBanner;
        private static readonly Vector2 DeskSize = new Vector2(1280, 720);
        internal static int LastBackFrame = -1;
        private RectTransform page;
        private readonly List<Button> buttons = new List<Button>();
        private int index, enteredFrame;
        private bool reviewing;
        private TMP_Text backPrompt;
        private Image backIcon;
        private string activeScheme;
        private static readonly Color Ink = new Color(.075f, .105f, .13f);
        private static readonly Color Paper = new Color32(229, 204, 161, 255);

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
            expectedOrder = Plugin.Book.Pending ? Plugin.Book.items : null;
            var cards = screen.GetComponentsInChildren<UICardItem>().Where(c => OfferIds.Available(CardId(c), Pool.Identify)).Take(3).ToArray();
            today = cards.Select(c => AddressablesManager.Instance.GetItem(CardId(c))).ToArray();
            todayPrices = cards.Select(c => Plugin.CardPrices.TryGetValue(c, out var costs)
                ? string.Join(" + ", costs.Select(cost => cost.amount + " " +
                    (RaceManager.Instance.GetResourceData(cost.type)?.GetName() ?? cost.type.ToString()))) : "").ToArray();
            RefreshMaterial();
            sample = merchant.GetComponentInChildren<TMP_Text>(true);
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            nativeButton = screen.transform.Find("Panel/Buttons/Minimize/Button")?.GetComponent<Button>();
            var cardTemplate = cards.FirstOrDefault()?.gameObject ?? DeliveryCards.ItemCardPrefab(screen);
            var art = cardTemplate == null ? Array.Empty<Image>() : cardTemplate.GetComponentsInChildren<Image>(true);
            cardFrame = art.FirstOrDefault(image => image.name == "Card" && image.sprite != null)?.sprite;
            cardBanner = art.FirstOrDefault(image => image.name == "Banner" && image.sprite != null)?.sprite;
            // The game's backdrop sits below its resource HUD. Keep that layer and
            // block clicks on our overlay without painting over the HUD a second time.
            Box("Input shield", transform, 0, 0, 1, 1).gameObject.AddComponent<Image>().color = Color.clear;
            foreach (Transform child in merchant.transform)
            {
                if (child.name == "Image") continue;
                var group = child.GetComponent<CanvasGroup>();
                bool added = group == null;
                if (added) group = child.gameObject.AddComponent<CanvasGroup>();
                hidden.Add((group, group.alpha, added));
                group.alpha = 0;
            }
            native = merchant.GetComponent<CanvasGroup>();
            addedGroup = native == null;
            if (addedGroup) native = merchant.gameObject.AddComponent<CanvasGroup>();
            oldInteractable = native.interactable;
            oldRaycasts = native.blocksRaycasts;
            native.interactable = native.blocksRaycasts = false;
            Render(0);
        }

        internal bool OwnsCurrentScreen => Plugin.IsCurrent(merchant);

        private void LateUpdate()
        {
            if (!OwnsCurrentScreen || !visit.Available) { Dismiss(); return; }
            FitDesk();
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

        private void FitDesk()
        {
            // Follow the two game UI sizes, keeping the full desk inside smaller displays.
            var bounds = ((RectTransform)transform).rect.size;
            float fit = Mathf.Min(bounds.x / DeskSize.x, bounds.y / DeskSize.y);
            float uiScale = SettingsManager.Instance.GetUIScaling();
            float top = Mathf.Min(88f * uiScale, bounds.y * .18f);
            float bottom = Mathf.Min(56f, bounds.y * .09f);
            float maxHeightScale = (bounds.y - top - bottom) / DeskSize.y;
            page.localScale = Vector3.one * Mathf.Min(fit * .70f * uiScale, maxHeightScale, fit * .96f);
            page.anchoredPosition = new Vector2(0, (bottom - top) * .5f);
        }

        private static Image Surface(RectTransform rect, Sprite sprite, Color color)
        {
            var face = rect.gameObject.AddComponent<Image>();
            face.sprite = sprite;
            if (sprite != null) face.type = Image.Type.Sliced;
            face.color = color;
            return face;
        }

        private TMP_Text Label(string name, RectTransform parent, string value, float left, float bottom,
            float right, float top, int maxSize, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var label = Box(name, parent, left, bottom, right, top).gameObject.AddComponent<TextMeshProUGUI>();
            if (sample != null) { label.font = sample.font; label.spriteAsset = sample.spriteAsset; }
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = maxSize;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.color = Ink;
            label.text = value;
            return label;
        }

        private void Column(string title, Item[] items, float left, float right, string[] prices = null)
        {
            var card = Box("Offer card", page, left, .234f, right, .773f);
            var frame = Surface(card, cardFrame, cardFrame != null ? Color.white : Paper);
            frame.pixelsPerUnitMultiplier = 4;
            var banner = Box("Banner", card, .008f, .773f, .992f, .974f);
            var ribbon = Surface(banner, cardBanner, cardBanner != null ? Color.white : (Color)new Color32(132, 57, 49, 255));
            ribbon.pixelsPerUnitMultiplier = 4;
            Label("Pool", banner, title, .05f, .14f, .95f, .9f, 30, TextAlignmentOptions.Center).color = Color.white;
            for (int i = 0; i < items.Length; i++)
            {
                float top = .77f - i * .227f;
                var item = items[i];
                if (item == null) continue;
                var icon = Box("Item icon", card, .062f, top - .175f, .155f, top - .01f).gameObject.AddComponent<Image>();
                icon.sprite = item.GetIcon();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Label("Item", card, "<b>" + item.GetTitle() + "</b>" +
                    (prices != null && prices[i].Length > 0 ? " (" + prices[i] + ")" : "") + "\n" + item.GetDescription(),
                    .18f, top - .215f, .944f, top, 23);
            }
        }

        private void Render(int focus)
        {
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            buttons.Clear();
            page = Box("Order desk", transform, .5f, .5f, .5f, .5f);
            page.sizeDelta = DeskSize;
            FitDesk();
            enteredFrame = Time.frameCount;
            var pool = pools.Count == 0 ? null : pools[index];
            int owned = Spendable();
            string name = material == null ? "material" : material.GetName();
            string cost = $"Cost: {Price} {name}     Available: {owned} {name}";
            if (Exchange) cost = "This order is paid. No additional materials are needed.";
            else if (owned < Price) cost += $"     Short by {Price - owned}";
            Label("Heading", page, reviewing ? "Confirm Commission" : "Caravan Commissions", .06f, .875f, .94f, .98f, 42, TextAlignmentOptions.Center).color = Color.white;
            string terms = Exchange ? "A promised item is unavailable. Keep your order, or exchange it for a future visit." :
                "Take one item today, or pay for a choice at the next caravan visit.";
            if (reviewing)
                terms = Exchange ? (pool == null ? "Close this order without an item or a refund?" :
                    $"Replace your paid order with {pool.Title} for the next visit at no extra cost?") :
                    $"Spend {Price} {name} and give up today's item for {pool.Title}?";
            Label("Terms", page, terms, .04f, .79f, .96f, .875f, 26, TextAlignmentOptions.Center).color = Color.white;
            Column(Exchange ? "Remaining Paid Choices" : "Today's Items", today, .028f, .484f, todayPrices);
            Column(pool == null ? "No Eligible Pool" : pool.Title + " (Next Visit)", pool?.Items ?? Array.Empty<Item>(), .516f, .972f);
            Label("Cost", page, cost, .04f, .158f, .96f, .229f, 27, TextAlignmentOptions.Center).color = Color.white;
            if (reviewing)
            {
                AddButton("Back to Pool", .16f, .47f, () => { reviewing = false; Render(2); });
                AddButton(Exchange ? (pool == null ? "Close Paid Order" : "Exchange for Free") : "Pay and Commission",
                    .53f, .84f, Pay, Exchange || owned >= Price);
            }
            else
            {
                AddButton("Previous Pool", .028f, .202f, () => Cycle(-1), pools.Count > 1);
                AddButton("Next Pool", .218f, .388f, () => Cycle(1), pools.Count > 1);
                AddButton(Exchange && pool == null ? "Close Paid Order" : "Review Order", .404f, .592f, () => { reviewing = true; Render(0); }, Exchange || (pool != null && owned >= Price));
                AddButton(Exchange ? "Back to Delivery" : "Today's Items", .608f, .785f, Dismiss);
                AddButton("Leave Caravan", .801f, .972f, Leave);
            }
            WireNavigation(focus);
            backIcon = PromptIcon(.447f);
            backPrompt = Label("Back prompt", page, "", .487f, -.02f, .72f, .045f, 20);
            backPrompt.color = Color.white;
            RefreshPrompts(GamepadManager.Instance.GetCurrentControlScheme());
        }

        private Image PromptIcon(float x)
        {
            var image = Box("Button prompt", page, x, -.009f, x + .033f, .04f).gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private void RefreshPrompts(string scheme)
        {
            activeScheme = scheme;
            var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            var actions = GamepadManager.Instance.GetPlayerInput()?.actions;
            string back = Binding(actions?.FindAction("UI/Cancel") ?? module?.cancel?.action, backIcon, scheme);
            backPrompt.rectTransform.anchorMin = new Vector2(backIcon.enabled ? .487f : 0, -.02f);
            backPrompt.rectTransform.anchorMax = new Vector2(backIcon.enabled ? .72f : 1, .045f);
            backPrompt.alignment = backIcon.enabled ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            backPrompt.text = (back + "  Back").Trim();
        }

        private static string Binding(InputAction action, Image image, string scheme)
        {
            // The shared keyboard/mouse scheme lists right-click before the keyboard Cancel binding.
            if (scheme == "KeyboardMouse" && action != null)
            {
                var key = action.controls.OfType<UnityEngine.InputSystem.Controls.KeyControl>().FirstOrDefault();
                if (key != null)
                {
                    image.enabled = false;
                    return key.displayName;
                }
            }
            int binding = action == null || string.IsNullOrEmpty(scheme) ? -1 : action.GetBindingIndex(group: scheme);
            var sprite = binding < 0 ? null : GamepadManager.Instance.GetButtonPrompt(action.bindings[binding].effectivePath);
            image.sprite = sprite;
            image.enabled = sprite != null;
            return binding < 0 || sprite != null ? "" : action.GetBindingDisplayString(binding);
        }

        private void Cycle(int step)
        {
            index = (index + step + pools.Count) % pools.Count;
            Render(step < 0 ? 0 : 1);
        }

        private void AddButton(string text, float left, float right, Action click, bool enabled = true)
        {
            var rect = Box(text, page, left, .056f, right, .144f);
            var sourceImage = nativeButton?.image;
            var face = Surface(rect, sourceImage?.sprite, sourceImage != null ? sourceImage.color : Color.white);
            if (sourceImage != null) face.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            if (nativeButton != null)
            {
                button.colors = nativeButton.colors;
                button.spriteState = nativeButton.spriteState;
                if (nativeButton.transition != Selectable.Transition.Animation)
                    button.transition = nativeButton.transition;
            }
            button.interactable = enabled;
            button.onClick.AddListener(() => { if (Time.frameCount != enteredFrame && visit.Available) click(); });
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;
            var input = rect.gameObject.AddComponent<PanelInput>();
            input.Owner = this;
            input.FocusOutline = outline;
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
            if (!visit.Available) { Dismiss(); return; }
            var pool = pools.Count == 0 ? null : pools[index];
            bool valid = pool != null && pool.Ids.All(id => OfferIds.Available(id, Pool.Identify));
            bool success;
            if (Exchange)
            {
                success = pool == null
                    ? Plugin.Book.items == expectedOrder && Plugin.Missing && Plugin.Book.Finish(visit.Day, visit.Number, true)
                    : valid && Plugin.Book.Exchange(visit.Day, visit.Number, expectedOrder, pool.Title, pool.Ids, Pool.Identify);
            }
            else
            {
                var quoted = materialType;
                var quotedSource = source;
                RefreshMaterial();
                success = valid && material != null && materialType == quoted && quotedSource == source &&
                    Plugin.Book.Place(visit.Day, visit.Number, pool.Title, pool.Ids, Debit);
            }
            if (!success)
            {
                pools = Pool.Eligible(visit.Guest);
                index = Math.Min(index, Math.Max(0, pools.Count - 1));
                reviewing = false;
                Render(2);
                return;
            }
            if (!Exchange) ResourceManager.Instance.ResourceAmountChanged(materialType);
            Dismiss();
            OverlayManager.Instance.Confirm();
        }

        private void RefreshMaterial()
        {
            var race = RaceManager.Instance;
            material = new[] { ResourceType.Wood, ResourceType.Stone, ResourceType.Board }
                .Select(race.GetResourceData).FirstOrDefault(data => data != null && data.type != ResourceType.None &&
                    race.GetBuildingTypeToTakeFrom(data.type) != SuperFantasyKingdom.Buildings.BuildingType.None);
            materialType = material == null ? ResourceType.None : material.type;
            source = material == null ? SuperFantasyKingdom.Buildings.BuildingType.None : race.GetBuildingTypeToTakeFrom(material.type);
        }

        private int Spendable()
        {
            if (material == null) return 0;
            var building = CityManager.Instance.GetBuilding(source);
            if (building != null) return CommissionPayment.Units(building.GetStorage().GetAllResourcePackets(), material.type).Length;
            return ResourceManager.Instance.GetStartingResourcesDictionary().TryGetValue(material.type, out var balance) ? balance : 0;
        }

        private bool Debit()
        {
            var resources = ResourceManager.Instance;
            var building = CityManager.Instance.GetBuilding(source);
            return building == null
                ? CommissionPayment.TakeStarting(resources.GetStartingResourcesDictionary(), material.type)
                : CommissionPayment.Take(CommissionPayment.Units(building.GetStorage().GetAllResourcePackets(), material.type), building.GetStorage(),
                    id => building.TakeResourcePacket(id), packet => building.AddResourcePacket(packet));
        }

        private void Leave()
        {
            Dismiss();
            Plugin.Leave(merchant, visit);
        }

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
            foreach (var entry in hidden)
            {
                if (entry.group == null) continue;
                entry.group.alpha = entry.alpha;
                if (entry.added) Destroy(entry.group);
            }
            gameObject.SetActive(false);
            var back = OwnsCurrentScreen ? merchant.transform.Find("Panel/Buttons/Cancel/Button") : null;
            if (back != null) Focus(back.gameObject);
            Destroy(gameObject);
        }
    }

    internal sealed class PanelInput : MonoBehaviour, ICancelHandler, ISelectHandler, IDeselectHandler
    {
        internal CommissionPanel Owner;
        internal Outline FocusOutline;
        public void OnSelect(BaseEventData e) => FocusOutline.enabled = true;
        public void OnDeselect(BaseEventData e) => FocusOutline.enabled = false;
        public void OnCancel(BaseEventData e) { if (Owner != null) Owner.Back(); e.Use(); }
    }
}
