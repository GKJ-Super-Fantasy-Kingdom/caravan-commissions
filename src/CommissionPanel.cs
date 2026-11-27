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
using UnityEngine.Localization.Components;
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
        private Button nativeButton, backTemplate;
        private Transform arrowTemplate;
        private const float CardWidth = 280f, CardHeight = 420f, CardGap = 24f;
        private static readonly Vector2 DeskSize = new Vector2(664, 630);
        private static readonly Vector2 FooterSize = new Vector2(230f, 36f);
        private const float FooterEdgeScale = 3f;
        internal static int LastBackFrame = -1;
        private RectTransform page;
        private int index, enteredFrame;
        private bool reviewing;
        private GameObject panelSelection;
        private RectTransform poolHints;
        private PoolShortcutHint previousHint, nextHint;
        private static readonly Color Ink = new Color(.075f, .105f, .13f);
        private static readonly Color Parchment = new Color32(229, 204, 161, 255);
        private static readonly Color Cream = new Color32(255, 245, 220, 255);
        private enum RecoveryArt { None, PaidItems, CloseOrder }

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
            var footer = Resources.Load<GameObject>("UI/OverlayMessage")?.transform.Find("Buttons");
            // Unit, relic, and caravan selection use this native cancel button.
            backTemplate = screen.transform.Find("Panel/Buttons/Cancel/Button")?.GetComponent<Button>() ?? nativeButton;
            sample = footer?.parent.Find("Message")?.GetComponent<TMP_Text>() ?? sample;
            arrowTemplate = Resources.Load<GameObject>("UI/OverlayElementWorker")?.transform.Find("Amount");
            // The game's backdrop sits below its resource HUD. Keep that layer and
            // block clicks on our overlay without painting over the HUD a second time.
            Box("Input shield", transform, 0, 0, 1, 1).gameObject.AddComponent<Image>().color = Color.clear;
            foreach (Transform child in merchant.transform)
            {
                var group = child.GetComponent<CanvasGroup>();
                bool added = group == null;
                if (added) group = child.gameObject.AddComponent<CanvasGroup>();
                hidden.Add((group, group.alpha, added));
                group.alpha = child.name == "Image" ? group.alpha * .78f : 0;
            }
            native = merchant.GetComponent<CanvasGroup>();
            addedGroup = native == null;
            if (addedGroup) native = merchant.gameObject.AddComponent<CanvasGroup>();
            oldInteractable = native.interactable;
            oldRaycasts = native.blocksRaycasts;
            native.interactable = native.blocksRaycasts = false;
            CreatePoolHints();
            Render();
        }

        internal bool OwnsCurrentScreen => Plugin.IsCurrent(merchant);

        private void LateUpdate()
        {
            if (!OwnsCurrentScreen || !visit.Available) { Dismiss(); return; }
            FitDesk();
            var pad = GamepadManager.Instance;
            var selected = EventSystem.current?.currentSelectedGameObject;
            var scheme = pad.GetCurrentControlScheme();
            if (selected != null && selected.transform.IsChildOf(page))
            {
                panelSelection = selected;
                if (pad.GetGameObjectToSelect() != selected) pad.SetGameObjectToSelect(selected, true);
            }
            else if (scheme == "Gamepad" && panelSelection != null && PauseMenuManger.Instance?.InMenu() != true)
                Focus(panelSelection);
            UpdatePoolShortcuts(pad, selected);
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
            float uiScale = SettingsManager.Instance.GetUIScaling();
            float top = Mathf.Min(88f * uiScale, bounds.y * .18f);
            float bottom = Mathf.Min(56f, bounds.y * .09f);
            float maxHeightScale = (bounds.y - top - bottom) / DeskSize.y;
            float maxWidthScale = bounds.x * .96f / DeskSize.x;
            float readableScale = Mathf.Approximately(uiScale, 1f) ? 1.2f : uiScale;
            page.localScale = Vector3.one * Mathf.Min(readableScale, maxHeightScale, maxWidthScale);
            page.anchoredPosition = new Vector2(0, (bottom - top) * .5f);
            poolHints.localScale = Vector3.one * Mathf.Min(uiScale, 1.2f, bounds.x * .94f / poolHints.rect.width);
        }

        private void CreatePoolHints()
        {
            poolHints = Box("Pool shortcuts", transform, 1f, 0f, 1f, 0f);
            poolHints.pivot = new Vector2(1f, 0f);
            // Box initially sets both offsets to zero. Size and place after setting the pivot.
            poolHints.sizeDelta = new Vector2(366f, 42f);
            poolHints.anchoredPosition = new Vector2(-16f, 14f);
            Surface(poolHints, null, new Color32(14, 17, 24, 226)).raycastTarget = false;
            previousHint = CreatePoolHint("Previous Pool", .014f, .54f);
            nextHint = CreatePoolHint("Next Pool", .54f, .986f);
            poolHints.gameObject.SetActive(false);
        }

        private PoolShortcutHint CreatePoolHint(string caption, float left, float right)
        {
            var row = Box(caption, poolHints, left, .04f, right, .96f);
            var captionLabel = Label("Action", row, caption, .355f, 0f, 1f, 1f, 22);
            captionLabel.color = Cream;
            var keyLabel = Label("Key name", row, "", .012f, 0f, .32f, 1f, 21, TextAlignmentOptions.Center);
            keyLabel.color = Cream;
            var glyph = Box("Game glyph", row, .04f, .06f, .31f, .94f).gameObject.AddComponent<Image>();
            glyph.raycastTarget = false;
            glyph.preserveAspect = true;
            return new PoolShortcutHint(row, keyLabel, glyph);
        }

        private void UpdatePoolShortcuts(GamepadManager pad, GameObject selected)
        {
            if (reviewing || pools.Count < 2 || PauseMenuManger.Instance?.InMenu() == true)
            {
                poolHints.gameObject.SetActive(false);
                return;
            }

            var player = pad.GetPlayerInput();
            var previous = player?.actions?.FindAction("Overlay/Left");
            var next = player?.actions?.FindAction("Overlay/Right");
            bool hasPrevious = previousHint.Refresh(previous, player, pad);
            bool hasNext = nextHint.Refresh(next, player, pad);
            poolHints.gameObject.SetActive(hasPrevious || hasNext);

            bool left = hasPrevious && previous.WasPerformedThisFrame();
            bool right = hasNext && next.WasPerformedThisFrame();
            if (left == right || Time.frameCount == enteredFrame) return;
            string focus = selected != null && selected.transform.IsChildOf(page) ? selected.name : null;
            Cycle(left ? -1 : 1, focus);
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
            if (sample != null)
            {
                label.font = sample.font;
                label.fontSharedMaterial = sample.fontSharedMaterial;
                label.spriteAsset = sample.spriteAsset;
            }
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

        private RectTransform ChoiceCard(string action, string title, Races kingdom, Item[] items, float center,
            out Button button, bool future, Action click = null, string[] nativeText = null, bool enabled = true,
            RecoveryArt recoveryArt = RecoveryArt.None)
        {
            float half = CardWidth / (2f * DeskSize.x);
            const float foot = .10f;
            var rect = Box(action, page, center - half, foot, center + half, foot + CardHeight / DeskSize.y);
            var sprite = future ? ChoiceFrame.Silver : ChoiceFrame.Bronze;
            var face = Surface(rect, sprite, sprite != null ? Color.white : (Color)new Color32(92, 54, 40, 255));
            face.type = Image.Type.Simple;
            face.raycastTarget = click != null;
            button = click == null ? null : AddControl(rect, face, click, enabled, ChoiceFrame.Rim(future));

            var art = Box("Kingdom illustration", rect, .0286f, .4762f, .9714f, .981f);
            var kingdomArt = ChoiceFrame.ForKingdom(kingdom);
            var landscape = Surface(art, kingdomArt.Scene, Color.white);
            landscape.type = Image.Type.Simple;
            landscape.raycastTarget = false;
            var resource = Helper.Instance.GetKingdomResource(kingdom);
            var crest = resource == ResourceType.None ? null : RaceManager.Instance.GetResourceIcon(resource);
            var caravan = CaravanIllustration.Get(kingdom);
            if (caravan != null)
            {
                var figure = Surface(Box("Kingdom caravan", art, .075f, .245f, .925f, .98f), caravan, Color.white);
                figure.type = Image.Type.Simple;
                figure.preserveAspect = true;
                figure.raycastTarget = false;
            }
            if (crest != null)
            {
                var crestRect = caravan != null
                    ? Box("Kingdom crest", art, .065f, .80f, .18f, .945f)
                    : Box("Kingdom crest", art, .23f, .30f, .77f, .89f);
                var emblem = Surface(crestRect, crest, Color.white);
                emblem.type = Image.Type.Simple;
                emblem.preserveAspect = true;
                emblem.raycastTarget = false;
            }
            if (recoveryArt != RecoveryArt.None)
                AddRecoveryIllustration(art, items, recoveryArt == RecoveryArt.CloseOrder);

            var nameplate = Box("Kingdom header", art, .035f, .025f, .965f, .259f);
            var plate = Surface(nameplate, kingdomArt.Plate, kingdomArt.Plate != null ? Color.white : (Color)new Color32(17, 15, 22, 217));
            plate.type = Image.Type.Simple;
            plate.raycastTarget = false;
            var kingdomName = Label("Pool", nameplate, title, .21f, .11f, .79f, .9f, 26, TextAlignmentOptions.Center);
            kingdomName.color = Cream;
            kingdomName.fontStyle = FontStyles.Bold;
            var caption = Label("Action", rect, action, .07f, .375f, .93f, .465f, 30, TextAlignmentOptions.Center);
            caption.color = Cream;
            caption.fontStyle = FontStyles.Bold;

            float rowTop = .354f;
            float rowHeight = future ? .087f : .106f;
            for (int i = 0; i < items.Length; i++)
            {
                float top = rowTop - i * rowHeight;
                var item = items[i];
                if (item == null) continue;
                var icon = Surface(Box("Item icon", rect, .056f, top - rowHeight * .85f, .149f, top - .004f), item.GetIcon(), Color.white);
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                string text = nativeText == null ? OfferText(item.GetTitle(), item.GetDescription()) : nativeText[i];
                // The item's native keyword colors are designed for light parchment.
                text = text.Replace("<color=blue>", "<color=#A9D2FF>").Replace("<color=purple>", "<color=#E4BBFF>");
                Label("Item", rect, text, .168f, top - rowHeight, .948f, top, 22).color = Cream;
            }
            string emptyText = recoveryArt == RecoveryArt.CloseOrder
                ? "Close the order without an item or refund."
                : recoveryArt == RecoveryArt.PaidItems && !items.Any(item => item != null)
                    ? "No paid items remain available." : null;
            if (emptyText != null)
                Label("Recovery details", rect, emptyText, .09f, .16f, .91f, .35f, 22,
                    TextAlignmentOptions.Center).color = Cream;
            return nameplate;
        }

        private void AddRecoveryIllustration(RectTransform art, Item[] items, bool closeOrder)
        {
            var icons = items.Where(item => item != null).Select(item => item.GetIcon())
                .Where(sprite => sprite != null).Take(2).ToArray();
            if (icons.Length == 0)
                icons = new[] { closeOrder ? RaceManager.Instance.GetResourceIcon(ResourceType.Banish) : ClosedChestSprite() };

            bool pair = icons.Length == 2;
            float halfWidth = pair ? .14f : .175f;
            float bottom = pair ? .43f : .38f;
            float top = pair ? .78f : .82f;
            var border = closeOrder ? new Color32(174, 100, 86, 255) : new Color32(195, 155, 105, 255);
            var fill = closeOrder ? new Color32(54, 28, 37, 255) : new Color32(39, 32, 45, 255);
            for (int i = 0; i < icons.Length; i++)
            {
                float center = pair ? .33f + .34f * i : .5f;
                var frame = Box("Recovery emblem", art, center - halfWidth, bottom, center + halfWidth, top);
                Surface(frame, null, border).raycastTarget = false;
                var inset = Box("Emblem inset", frame, .045f, .045f, .955f, .955f);
                Surface(inset, null, fill).raycastTarget = false;
                var sprite = icons[i];
                if (sprite != null)
                {
                    var image = Surface(Box("Recovery icon", inset, .15f, .15f, .85f, .85f), sprite, Color.white);
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
                else
                    Label("Recovery symbol", inset, closeOrder ? "X" : "?", 0f, 0f, 1f, 1f, 40,
                        TextAlignmentOptions.Center).color = Cream;
            }
        }

        private static Sprite ClosedChestSprite()
        {
            // The Chest resource icon depicts the portal. Use the closed world-drop chest frame.
            var frames = PrefabsManager.Instance?.GetDropChest()?.GetComponent<SpriteAnimator>()?.GetSprites();
            return frames?.FirstOrDefault(sprite => sprite != null && sprite.name == "Chest_0")
                ?? Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sprite => sprite.name == "Chest_0");
        }

        private void PriceLine(RectTransform card, int owned)
        {
            if (Exchange || pools.Count == 0 || material == null)
            {
                string cost = Exchange ? "Already Paid" : pools.Count == 0 ? "No Complete Pool" : "No Payment Material";
                Label("Cost", card, cost, .09f, .036f, .91f, .092f, 19, TextAlignmentOptions.Center).color = Cream;
                return;
            }

            if (material.icon != null)
            {
                var icon = Surface(Box("Payment material", card, .07f, .038f, .155f, .092f), material.icon, Color.white);
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            Label("Cost", card, $"Pay {Price} {material.GetName()}", .167f, .035f, .52f, .092f, 21).color = Cream;
            string stock = owned < Price ? $"In stock: {owned} · need {Price - owned} more" : $"In stock: {owned}";
            Label("Stock", card, stock, .51f, .035f, .94f, .092f, 21, TextAlignmentOptions.Right).color =
                owned < Price ? (Color)new Color32(255, 209, 134, 255) : Cream;
        }

        private void Render(string focus = null)
        {
            bool keepKeyboardFocus = page != null && PanelInput.HasKeyboardFocus(page);
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            page = Box("Order desk", transform, .5f, .5f, .5f, .5f);
            page.sizeDelta = DeskSize;
            FitDesk();
            enteredFrame = Time.frameCount;
            var pool = pools.Count == 0 ? null : pools[index];
            bool closingPaidOrder = Exchange && pool == null;
            string title = pool == null ? "No Eligible Pool" : pool.Title;
            var kingdom = pool?.Kingdom ?? Races.None;
            var items = pool?.Items ?? Array.Empty<Item>();
            int owned = Spendable();

            var heading = Label("Heading", page, reviewing ? "Confirm Commission" : "Caravan Commissions",
                .035f, .905f, .965f, .987f, 40, TextAlignmentOptions.Center);
            heading.color = Cream;
            heading.fontStyle = FontStyles.Bold;
            string terms = "Take one item today, or reserve one for the next item caravan.";
            if (Exchange)
            {
                bool hasPaidItems = today.Any(item => item != null);
                if (closingPaidOrder)
                    terms = hasPaidItems ? "Choose a remaining paid item or close the order." :
                        "No paid items or replacement pools are available.";
                else
                    terms = hasPaidItems ? "Choose a remaining paid item or review a replacement pool." :
                        "Review a replacement pool at no extra cost.";
            }
            if (reviewing)
                terms = Exchange ? (closingPaidOrder ? "Close this order without an item or a refund?" :
                    "Replace your paid order with these choices at no extra cost?") :
                    "Give up today's pick to reserve one of these items for the next item caravan?";
            Label("Terms", page, terms, .035f, .815f, .965f, .895f, 25, TextAlignmentOptions.Center).color = Cream;

            if (reviewing)
            {
                var banner = closingPaidOrder
                    ? ChoiceCard("Paid Choices", "Current Order", Races.None, today, .5f, out _, future: true,
                        nativeText: todayText, recoveryArt: RecoveryArt.PaidItems)
                    : ChoiceCard("Reserved Choices", title, kingdom, items, .5f, out _, future: true);
                PriceLine((RectTransform)banner.parent.parent, owned);
                const float gap = 16f;
                float halfSpacing = (FooterSize.x + gap) / (2f * DeskSize.x);
                var action = closingPaidOrder
                    ? CancelFooter("Close Paid Order", .5f + halfSpacing, Pay)
                    : Footer(Exchange ? "Exchange for Free" : "Pay and Commission", .5f + halfSpacing,
                        Pay, nativeButton, Exchange || (material != null && owned >= Price));
                var back = BackButton(.5f - halfSpacing);
                Navigate(action, null, null, back, null);
                Navigate(back, null, null, null, action);
                FocusPanel(action.interactable && !closingPaidOrder ? action : back, keepKeyboardFocus);
            }
            else
            {
                var guestKingdom = Exchange ? Races.None : Pool.VisitingKingdom(visit.Guest);
                string guestTitle = Exchange ? "Current Order" : Helper.Instance.GetKingdomTitle(guestKingdom);
                float halfGap = (CardWidth + CardGap) / (2f * DeskSize.x);
                ChoiceCard(Exchange ? "Back to Delivery" : "Today's Items", guestTitle, guestKingdom, today,
                    .5f - halfGap, out var immediate, false, ReturnToItems, todayText,
                    recoveryArt: Exchange ? RecoveryArt.PaidItems : RecoveryArt.None);
                bool paging = pools.Count > 1;
                var banner = ChoiceCard(closingPaidOrder ? "Close Paid Order" : "Review Order", title, kingdom, items,
                    .5f + halfGap, out var review, true, () => { reviewing = true; Render(); }, enabled: Exchange || pool != null,
                    recoveryArt: closingPaidOrder ? RecoveryArt.CloseOrder : RecoveryArt.None);
                PriceLine((RectTransform)banner.parent.parent, owned);
                Button prev = null, next = null;
                if (paging)
                {
                    prev = Arrow(banner, "Previous Pool", "Less", -1);
                    next = Arrow(banner, "Next Pool", "More", 1);
                }

                var back = BackButton(.5f);
                var primary = review.interactable ? review : immediate;
                Navigate(immediate, null, back, null, review);
                Navigate(review, next, back, immediate, null);
                Navigate(prev, null, primary, immediate, next);
                Navigate(next, null, primary, prev, null);
                Navigate(back, primary, null, immediate, review);
                Button initial = new[] { prev, next, immediate, review, back }.FirstOrDefault(
                    button => button != null && button.interactable && button.name == focus);
                FocusPanel(initial ?? primary, keepKeyboardFocus);
            }
        }

        private Button Arrow(RectTransform parent, string name, string nativeName, int step)
        {
            float left = step < 0 ? .015f : .867f;
            var slot = Box(name, parent, left, .10f, left + .118f, .90f);
            var template = arrowTemplate?.Find(nativeName)?.GetComponent<Button>();
            if (template == null)
                return PlateControl(name, step < 0 ? "<" : ">", slot, () => Cycle(step), nativeButton);

            var button = Instantiate(template, slot, false);
            var rect = (RectTransform)button.transform;
            rect.name = name;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(26f, 32f);
            rect.anchoredPosition = Vector2.zero;
            button.onClick = new Button.ButtonClickedEvent();
            BindControl(rect, button, () => Cycle(step), true);
            var arrowRim = ChoiceFrame.ArrowRim(step < 0);
            if (arrowRim != null && button.targetGraphic is Image glyph)
            {
                var rim = Surface(Box("Arrow focus edge", glyph.rectTransform, -1f / 6f, -.125f, 7f / 6f, 1.125f),
                    arrowRim, Color.white);
                rim.type = Image.Type.Simple;
                rim.raycastTarget = false;
                button.GetComponent<PanelInput>().SetRim(rim);
            }
            return button;
        }

        private Button BackButton(float center) =>
            CancelFooter(reviewing ? "Back to Pool" : "Back", center, Back, caption: "Back");

        private Button CancelFooter(string name, float center, Action click, string caption = null)
        {
            var button = Footer(name, center, click, backTemplate, caption: caption);
            if (button.image is Image plate && plate.sprite?.name == "UI_23")
            {
                // Preserve the desk's deep red fill with the native resting plate.
                plate.color = new Color32(164, 184, 255, 255);
            }
            return button;
        }

        private void Cycle(int step, string focus = null)
        {
            index = (index + step + pools.Count) % pools.Count;
            Render(focus ?? (step < 0 ? "Previous Pool" : "Next Pool"));
        }

        private Button Footer(string name, float center, Action click, Button template,
            bool enabled = true, string caption = null)
        {
            var slot = Box("Footer slot", page, center, .036f, center, .036f);
            slot.sizeDelta = FooterSize;
            if (template == null)
                return PlateControl(name, caption ?? name, slot, click, nativeButton, enabled, FooterEdgeScale);
            var control = Instantiate(template, slot, false);
            var rect = (RectTransform)control.transform;
            rect.name = name;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = FooterSize;
            // A cloned selector may carry a native shortcut to its original merchant action.
            foreach (var prompt in rect.GetComponentsInChildren<UIButtonPrompt>(true)) prompt.enabled = false;
            var text = rect.Find("Text")?.GetComponent<TMP_Text>();
            if (text != null)
            {
                var localized = text.GetComponent<LocalizeStringEvent>();
                if (localized != null) localized.enabled = false;
                text.gameObject.SetActive(true);
                text.text = caption ?? name;
                text.enableAutoSizing = true;
                text.fontSizeMin = 14f;
                text.fontSizeMax = 20f;
            }
            control.onClick = new Button.ButtonClickedEvent();
            BindControl(rect, control, click, enabled);
            ApplyPlateEdge(control, FooterEdgeScale);
            control.gameObject.SetActive(true);
            return control;
        }

        private Button PlateControl(string name, string caption, RectTransform rect, Action click, Button template,
            bool enabled = true, float edgeScale = 1f)
        {
            rect.name = name;
            var sourceImage = (template ?? nativeButton)?.targetGraphic as Image;
            var face = Surface(rect, sourceImage?.sprite, sourceImage != null ? sourceImage.color : Parchment);
            if (sourceImage != null) face.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
            var button = AddControl(rect, face, click, enabled);
            if (template != null)
            {
                button.colors = template.colors;
                button.spriteState = template.spriteState;
                if (template.transition != Selectable.Transition.Animation) button.transition = template.transition;
            }
            if (caption.Length > 0)
                Label("Label", rect, caption, .035f, .04f, .965f, .96f, 23, TextAlignmentOptions.Center).color =
                    template != null && template == backTemplate ? Cream : Ink;
            ApplyPlateEdge(button, edgeScale);
            return button;
        }

        private static void ApplyPlateEdge(Button button, float edgeScale)
        {
            if (button.transition != Selectable.Transition.SpriteSwap || !(button.targetGraphic is Image plate))
                return;
            var states = button.spriteState;
            var edge = states.selectedSprite;
            if (edge == null || edge.border == Vector4.zero) return;

            // Draw only the native selected edge. The plate keeps its native hover, press, and disabled fills.
            plate.pixelsPerUnitMultiplier *= edgeScale;
            states.selectedSprite = plate.sprite;
            button.spriteState = states;

            var rim = Surface(Box("Button focus edge", plate.rectTransform, 0, 0, 1, 1), edge, Color.white);
            rim.fillCenter = false;
            rim.pixelsPerUnitMultiplier = plate.pixelsPerUnitMultiplier;
            rim.raycastTarget = false;
            button.GetComponent<PanelInput>().SetRim(rim);
        }

        private Button AddControl(RectTransform rect, Image face, Action click, bool enabled, Sprite ring = null)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            if (ring != null)
            {
                button.transition = Selectable.Transition.None;
                if (!enabled) face.color = new Color(.72f, .72f, .72f);
            }
            BindControl(rect, button, click, enabled);
            if (ring != null)
            {
                var rim = Surface(Box("Selection rim", rect, 0, 0, 1, 1), ring, Color.white);
                rim.type = Image.Type.Simple;
                rim.raycastTarget = false;
                rim.rectTransform.offsetMin = new Vector2(-4f, -4f);
                rim.rectTransform.offsetMax = new Vector2(4f, 4f);
                rect.GetComponent<PanelInput>().SetRim(rim, showDarkAtRest: true);
            }
            return button;
        }

        private void BindControl(RectTransform rect, Button button, Action click, bool enabled)
        {
            button.interactable = enabled;
            button.onClick.AddListener(() => { if (button.IsInteractable() && Time.frameCount != enteredFrame && visit.Available) click(); });
            rect.gameObject.AddComponent<PanelInput>().Owner = this;
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

        private void FocusPanel(Button button, bool keepKeyboardFocus)
        {
            panelSelection = button.gameObject;
            Focus(panelSelection);
            if (keepKeyboardFocus) button.GetComponent<PanelInput>().RestoreKeyboardFocus();
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

    internal sealed class PanelInput : MonoBehaviour, ICancelHandler, ISelectHandler, IDeselectHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
    {
        private static PanelInput keyboardFocus;
        private static readonly Color DarkEdge = new Color32(6, 6, 8, 255);
        internal CommissionPanel Owner;
        private Button button;
        private Image rim;
        private PointerEventData pointer;
        private bool darkAtRest, pressed;
        private float submitUntil;

        internal static bool HasKeyboardFocus(Transform page) =>
            keyboardFocus != null && keyboardFocus.transform.IsChildOf(page) &&
            EventSystem.current?.currentSelectedGameObject == keyboardFocus.gameObject &&
            GamepadManager.Instance?.IsGamepadInUse() != true;

        internal void RestoreKeyboardFocus()
        {
            keyboardFocus = this;
            Refresh();
        }

        internal void SetRim(Image image, bool showDarkAtRest = false)
        {
            button = GetComponent<Button>();
            rim = image;
            darkAtRest = showDarkAtRest;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        public void OnSelect(BaseEventData e)
        {
            if (e is AxisEventData && GamepadManager.Instance?.IsGamepadInUse() != true)
                RestoreKeyboardFocus();
            else
                Refresh();
        }

        public void OnDeselect(BaseEventData e)
        {
            if (keyboardFocus == this) keyboardFocus = null;
            submitUntil = 0f;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (GamepadManager.Instance?.IsGamepadInUse() != true) keyboardFocus = null;
            pointer = e;
            Refresh();
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (pointer == e) pointer = null;
            Refresh();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (GamepadManager.Instance?.IsGamepadInUse() != true) keyboardFocus = null;
            pressed = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressed = false;
            Refresh();
        }

        public void OnSubmit(BaseEventData e)
        {
            if (button != null && button.IsInteractable())
                submitUntil = Time.unscaledTime + Mathf.Max(.1f, button.colors.fadeDuration);
            Refresh();
        }

        private void Refresh()
        {
            if (rim == null) return;
            bool selected = EventSystem.current?.currentSelectedGameObject == gameObject;
            bool gamepad = GamepadManager.Instance?.IsGamepadInUse() == true;
            if (gamepad && keyboardFocus == this) keyboardFocus = null;
            bool inputFocus = selected && (gamepad || keyboardFocus == this);
            // Pointer enter also reaches the card containing the pool arrows. Light the closest button only.
            var hit = pointer?.pointerCurrentRaycast.gameObject;
            bool hovering = !gamepad && hit != null && hit.GetComponentInParent<Button>() == button;
            bool lit = button.IsInteractable() && (hovering || pressed || inputFocus || Time.unscaledTime < submitUntil);
            rim.enabled = lit || darkAtRest;
            var color = lit ? Color.white : DarkEdge;
            if (rim.color != color) rim.color = color;
        }

        private void OnDisable()
        {
            if (keyboardFocus == this) keyboardFocus = null;
            pointer = null;
            pressed = false;
            submitUntil = 0f;
        }

        public void OnCancel(BaseEventData e) { if (Owner != null) Owner.Back(); e.Use(); }
    }
}
