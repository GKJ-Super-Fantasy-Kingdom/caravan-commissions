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
        private static readonly AccessTools.FieldRef<UICardItem, TextMeshProUGUI> CardDescription =
            AccessTools.FieldRefAccess<UICardItem, TextMeshProUGUI>("description");
        private static readonly AccessTools.FieldRef<UICardItem, TextMeshProUGUI> CardTitle =
            AccessTools.FieldRefAccess<UICardItem, TextMeshProUGUI>("title");
        private string[] todayText;
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
        private Button nativeButton, acceptTemplate;
        private Sprite cardFrame, cardBanner;
        private static readonly Vector2 DeskSize = new Vector2(1280, 720);
        internal static int LastBackFrame = -1;
        private RectTransform page;
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
            todayText = cards.Select(ImmediateOfferText).ToArray();
            RefreshMaterial();
            sample = merchant.GetComponentInChildren<TMP_Text>(true);
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            nativeButton = screen.transform.Find("Panel/Buttons/Minimize/Button")?.GetComponent<Button>();
            acceptTemplate = Resources.Load<GameObject>("UI/OverlayMessage")?.transform
                .Find("Buttons/BuyButton")?.GetComponent<Button>();
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
            Render();
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

        private static string ImmediateOfferText(UICardItem card)
        {
            // Read after the native picker has finished. Other mods can add terms such as
            // the Merchant Charter surcharge directly to the visible description.
            Plugin.CardPrices.TryGetValue(card, out var costs);
            var item = AddressablesManager.Instance.GetItem(CardId(card));
            return OfferText(CardTitle(card)?.text ?? item.GetTitle(),
                CardDescription(card)?.text ?? item.GetDescription(), costs);
        }

        internal static string OfferText(string title, string description, ResourceAmount[] costs = null)
        {
            if (costs != null && costs.Length > 0)
            {
                // The native buy button uses the first entry for its icon, amount and debit.
                // Further entries can gate affordability without increasing the displayed price.
                var price = costs[0];
                var resource = RaceManager.Instance.GetResourceData(price.type);
                title += $" ({price.amount} {resource?.GetName() ?? price.type.ToString()})";
            }
            return "<b>" + title + "</b>\n" + description;
        }

        private RectTransform Column(string title, Item[] items, float left, float right, string[] nativeText = null, bool paging = false)
        {
            var card = Box("Offer card", page, left, .27f, right, .785f);
            var frame = Surface(card, cardFrame, cardFrame != null ? Color.white : Paper);
            frame.pixelsPerUnitMultiplier = 4;
            var banner = Box("Banner", card, .008f, .773f, .992f, .974f);
            var ribbon = Surface(banner, cardBanner, cardBanner != null ? Color.white : (Color)new Color32(132, 57, 49, 255));
            ribbon.pixelsPerUnitMultiplier = 4;
            Label("Pool", banner, title, paging ? .16f : .05f, .14f, paging ? .84f : .95f, .9f, 30, TextAlignmentOptions.Center).color = Color.white;
            for (int i = 0; i < items.Length; i++)
            {
                float top = .77f - i * .227f;
                var item = items[i];
                if (item == null) continue;
                var icon = Box("Item icon", card, .062f, top - .175f, .155f, top - .01f).gameObject.AddComponent<Image>();
                icon.sprite = item.GetIcon();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Label("Item", card, nativeText == null ? OfferText(item.GetTitle(), item.GetDescription()) : nativeText[i],
                    .18f, top - .215f, .944f, top, 23);
            }
            return banner;
        }

        private void PriceLine(int owned, float left, float right)
        {
            var row = Box("Commission price", page, left, .194f, right, .257f);
            var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = true;
            group.childControlHeight = group.childForceExpandWidth = group.childForceExpandHeight = false;
            group.spacing = 20;

            void Text(string id, string value, Color color)
            {
                var label = Label(id, row, value, 0, .5f, 0, .5f, 26, TextAlignmentOptions.Center);
                label.rectTransform.sizeDelta = new Vector2(0, 43);
                label.color = color;
            }

            if (Exchange || pools.Count == 0 || material == null)
            {
                Text("Cost", Exchange ? "Already paid" : pools.Count == 0 ? "No complete pool to commission this visit" :
                    "No payment material available", Color.white);
                return;
            }
            if (material.icon != null)
            {
                var icon = Box("Payment material", row, 0, .5f, 0, .5f);
                icon.sizeDelta = new Vector2(32, 32);
                var image = icon.gameObject.AddComponent<Image>();
                image.sprite = material.icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                icon.gameObject.AddComponent<LayoutElement>().preferredWidth = 32;
            }
            Text("Cost", $"Pay now: {Price} {material.GetName()}", Color.white);
            Text("Stock", owned < Price ? $"In stock: {owned} (need {Price - owned} more)" : $"In stock: {owned}",
                owned < Price ? (Color)new Color32(255, 209, 134, 255) : Color.white);
        }

        private void Render(string focus = null)
        {
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            page = Box("Order desk", transform, .5f, .5f, .5f, .5f);
            page.sizeDelta = DeskSize;
            FitDesk();
            enteredFrame = Time.frameCount;
            var pool = pools.Count == 0 ? null : pools[index];
            int owned = Spendable();
            Label("Heading", page, reviewing ? "Confirm Commission" : "Caravan Commissions", .06f, .89f, .94f, .98f, 40, TextAlignmentOptions.Center).color = Color.white;
            string terms = Exchange ? "A promised item is unavailable. Keep your order, or exchange it for a future visit." :
                "Take one item today, or reserve a choice for the next item caravan.";
            if (reviewing)
                terms = Exchange ? (pool == null ? "Close this order without an item or a refund?" :
                    "Replace your paid order with these choices at no extra cost?") :
                    "Give up today's pick to choose one of these items at the next caravan?";
            Label("Terms", page, terms, .04f, .806f, .96f, .887f, 26, TextAlignmentOptions.Center).color = Color.white;

            string title = pool == null ? "No Eligible Pool" : pool.Title + " (Next Caravan)";
            var items = pool?.Items ?? Array.Empty<Item>();
            if (reviewing)
            {
                if (Exchange && pool == null)
                    Column("Remaining Paid Choices", today, .256f, .744f, todayText);
                else
                    Column(title, items, .256f, .744f);
                PriceLine(owned, .23f, .77f);
                var pay = ActionButton(Exchange ? (pool == null ? "Close Paid Order" : "Exchange for Free") : "Pay and Commission",
                    .37f, .63f, Pay, true, Exchange || (material != null && owned >= Price));
                var back = BackButton();
                Navigate(pay, back, back, back, back);
                Navigate(back, pay, pay, pay, pay);
                Focus(pay.interactable && !(Exchange && pool == null) ? pay.gameObject : back.gameObject);
            }
            else
            {
                Column(Exchange ? "Remaining Paid Choices" : "Choose One Today", today, .028f, .484f, todayText);
                bool paging = pools.Count > 1;
                var banner = Column(title, items, .516f, .972f, paging: paging);
                PriceLine(owned, .513f, .975f);
                var immediate = ActionButton(Exchange ? "Back to Delivery" : "Today's Items", .156f, .356f, ReturnToItems);
                var review = ActionButton(Exchange && pool == null ? "Close Paid Order" : "Review Order", .634f, .854f,
                    () => { reviewing = true; Render(); }, true, Exchange || pool != null);
                Button prev = null, next = null;
                if (paging)
                {
                    prev = Control("Previous Pool", "<", Box("Previous", banner, .024f, .20f, .131f, .80f), () => Cycle(-1));
                    next = Control("Next Pool", ">", Box("Next", banner, .869f, .20f, .976f, .80f), () => Cycle(1));
                }
                var leave = Control("Leave Caravan", "Leave Caravan", Box("Leave", page, .028f, .003f, .181f, .059f), Leave, quiet: true);
                var back = BackButton();
                var primary = review.interactable ? review : immediate;
                Navigate(immediate, prev, leave, null, review);
                Navigate(review, next, back, immediate, null);
                Navigate(prev, null, primary, next, next);
                Navigate(next, null, primary, prev, prev);
                Navigate(leave, immediate, null, null, back);
                Navigate(back, primary, null, leave, null);
                var initial = focus == "Previous Pool" ? prev : focus == "Next Pool" ? next : primary;
                Focus((initial ?? primary).gameObject);
            }
            RefreshPrompts(GamepadManager.Instance.GetCurrentControlScheme());
        }

        private Button BackButton()
        {
            var rect = Box("Return", page, .844f, .002f, .972f, .065f);
            var button = Control(reviewing ? "Back to Pool" : "Back", "", rect, Back, quiet: true);
            backIcon = Box("Button prompt", rect, .10f, .16f, .34f, .84f).gameObject.AddComponent<Image>();
            backIcon.preserveAspect = true;
            backIcon.raycastTarget = false;
            backPrompt = Label("Back prompt", rect, "", .37f, .06f, .95f, .94f, 22);
            backPrompt.color = Color.white;
            return button;
        }

        private void RefreshPrompts(string scheme)
        {
            activeScheme = scheme;
            var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            var actions = GamepadManager.Instance.GetPlayerInput()?.actions;
            string back = Binding(actions?.FindAction("UI/Cancel") ?? module?.cancel?.action, backIcon, scheme);
            backPrompt.rectTransform.anchorMin = new Vector2(backIcon.enabled ? .37f : .04f, .06f);
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
            Render(step < 0 ? "Previous Pool" : "Next Pool");
        }

        private Button ActionButton(string name, float left, float right, Action click, bool primary = false, bool enabled = true)
        {
            return Control(name, name, Box(name, page, left, .105f, right, .183f), click, primary, enabled);
        }

        private Button Control(string name, string caption, RectTransform rect, Action click, bool primary = false,
            bool enabled = true, bool quiet = false)
        {
            rect.name = name;
            var template = primary && acceptTemplate != null ? acceptTemplate : nativeButton;
            var sourceImage = template?.targetGraphic as Image;
            var face = Surface(rect, sourceImage?.sprite, quiet ? (Color)new Color32(29, 28, 32, 245) :
                sourceImage != null ? sourceImage.color : Color.white);
            if (sourceImage != null) face.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier * (quiet ? 2f : 1.5f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            if (template != null)
            {
                button.colors = template.colors;
                button.spriteState = template.spriteState;
                if (template.transition != Selectable.Transition.Animation)
                    button.transition = template.transition;
            }
            if (primary)
            {
                // The prefab swaps gray sprites on focus. Tint its normal face so the
                // prepaid action stays distinct from an unavailable payment.
                button.transition = Selectable.Transition.ColorTint;
                face.color = Color.white;
                var colors = button.colors;
                colors.normalColor = new Color32(255, 228, 166, 255);
                colors.highlightedColor = colors.selectedColor = new Color32(255, 196, 80, 255);
                colors.pressedColor = new Color32(235, 157, 65, 255);
                colors.disabledColor = new Color32(167, 167, 167, 180);
                colors.colorMultiplier = 1f;
                button.colors = colors;
            }
            button.interactable = enabled;
            button.onClick.AddListener(() => { if (Time.frameCount != enteredFrame && visit.Available) click(); });
            // Keep the focus border independent of the face's native sprite swap or tint.
            var border = Box("Focus", rect.parent, rect.anchorMin.x, rect.anchorMin.y, rect.anchorMax.x, rect.anchorMax.y);
            border.offsetMin = rect.offsetMin - new Vector2(3f, 3f);
            border.offsetMax = rect.offsetMax + new Vector2(3f, 3f);
            border.SetSiblingIndex(rect.GetSiblingIndex());
            border.gameObject.AddComponent<Image>().raycastTarget = false;
            border.gameObject.SetActive(false);
            var input = rect.gameObject.AddComponent<PanelInput>();
            input.Owner = this;
            input.FocusBorder = border.gameObject;
            if (caption.Length > 0)
            {
                var label = Label("Label", rect, caption, .025f, .03f, .975f, .97f, quiet ? 20 : 24, TextAlignmentOptions.Center);
                label.color = quiet ? Color.white : Ink;
            }
            return button;
        }

        private static void Navigate(Button button, Button up, Button down, Button left, Button right)
        {
            if (button == null) return;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = up != null && up.interactable ? up : null,
                selectOnDown = down != null && down.interactable ? down : null,
                selectOnLeft = left != null && left.interactable ? left : null,
                selectOnRight = right != null && right.interactable ? right : null };
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
            if (reviewing) { reviewing = false; Render(); }
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
                Render();
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

        private void ReturnToItems()
        {
            Dismiss();
            var first = merchant.GetComponentsInChildren<UICardItem>().Select(card => card.GetCardButton())
                .FirstOrDefault(button => button != null && button.isActiveAndEnabled && button.IsInteractable());
            if (first != null) Focus(first.gameObject);
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
        internal GameObject FocusBorder;
        public void OnSelect(BaseEventData e) => FocusBorder.SetActive(true);
        public void OnDeselect(BaseEventData e) => FocusBorder.SetActive(false);
        public void OnCancel(BaseEventData e) { if (Owner != null) Owner.Back(); e.Use(); }
    }
}
