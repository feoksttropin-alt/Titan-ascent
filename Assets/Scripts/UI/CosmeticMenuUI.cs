using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TitanAscent.Data;
using TitanAscent.Systems;

namespace TitanAscent.UI
{
    /// <summary>
    /// Cosmetic selection menu. Shows 4 tabs (Suits, Grapple Skins, Rope Colors,
    /// Particle Trails) each containing a scrollable grid of cosmetic items.
    /// </summary>
    public class CosmeticMenuUI : MonoBehaviour
    {
        // ── Inspector references ──────────────────────────────────────────────────
        [Header("Systems")]
        [SerializeField] private CosmeticSystem cosmeticSystem;
        [SerializeField] private SaveManager saveManager;

        [Header("Tab Buttons")]
        [SerializeField] private Button tabSuitsButton;
        [SerializeField] private Button tabGrappleSkinButton;
        [SerializeField] private Button tabRopeColorButton;
        [SerializeField] private Button tabParticleTrailButton;

        [Header("Grid")]
        [SerializeField] private ScrollRect gridScrollRect;
        [SerializeField] private Transform gridContent;
        [SerializeField] private GameObject itemCellPrefab;

        [Header("Preview")]
        [SerializeField] private RawImage previewRenderTexture;   // optional 3D preview
        [SerializeField] private Image    previewSpriteImage;     // fallback sprite preview

        [Header("Info Panel")]
        [SerializeField] private TextMeshProUGUI unlockConditionText;

        [Header("Footer")]
        [SerializeField] private Button saveLoadoutButton;

        // ── State ─────────────────────────────────────────────────────────────────
        private CosmeticType activeTab = CosmeticType.Suit;
        private CosmeticLoadout pendingLoadout = new CosmeticLoadout();

        // Tracks which cell is currently "equipped" per tab so we can badge it
        private readonly Dictionary<CosmeticType, string> equippedIdPerTab =
            new Dictionary<CosmeticType, string>
            {
                { CosmeticType.Suit,          string.Empty },
                { CosmeticType.GrappleSkin,   string.Empty },
                { CosmeticType.RopeColor,     string.Empty },
                { CosmeticType.ParticleTrail, string.Empty },
            };

        private CosmeticItem[] allCosmetics;
        private readonly List<GameObject> spawnedCells = new List<GameObject>();

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (cosmeticSystem == null)
                cosmeticSystem = FindFirstObjectByType<CosmeticSystem>();
            if (saveManager == null)
                saveManager = FindFirstObjectByType<SaveManager>();

            allCosmetics = Resources.LoadAll<CosmeticItem>("Cosmetics");

            tabSuitsButton?.onClick.AddListener(() => SwitchTab(CosmeticType.Suit));
            tabGrappleSkinButton?.onClick.AddListener(() => SwitchTab(CosmeticType.GrappleSkin));
            tabRopeColorButton?.onClick.AddListener(() => SwitchTab(CosmeticType.RopeColor));
            tabParticleTrailButton?.onClick.AddListener(() => SwitchTab(CosmeticType.ParticleTrail));

            saveLoadoutButton?.onClick.AddListener(OnSaveLoadoutClicked);

            SwitchTab(CosmeticType.Suit);
        }

        // ── Tab switching ─────────────────────────────────────────────────────────

        private void SwitchTab(CosmeticType type)
        {
            activeTab = type;
            ClearGrid();
            PopulateGrid(type);

            if (gridScrollRect != null)
                gridScrollRect.normalizedPosition = new Vector2(0f, 1f);

            if (unlockConditionText != null)
                unlockConditionText.text = string.Empty;
        }

        // ── Grid population ───────────────────────────────────────────────────────

        private void PopulateGrid(CosmeticType type)
        {
            foreach (CosmeticItem item in allCosmetics)
            {
                if (item.itemType != type) continue;

                GameObject cell = Instantiate(itemCellPrefab, gridContent);
                spawnedCells.Add(cell);
                SetupCell(cell, item);
            }
        }

        private void SetupCell(GameObject cell, CosmeticItem item)
        {
            bool isUnlocked = item.isUnlockedByDefault ||
                              (saveManager != null && saveManager.IsCosmeticUnlocked(item.GetId()));

            string equippedId = equippedIdPerTab.ContainsKey(item.itemType)
                ? equippedIdPerTab[item.itemType]
                : string.Empty;

            bool isEquipped = equippedId == item.GetId();

            // Preview image
            Image preview = cell.transform.Find("PreviewImage")?.GetComponent<Image>();
            if (preview != null && item.previewSprite != null)
                preview.sprite = item.previewSprite;

            // Item name
            TextMeshProUGUI nameLabel = cell.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameLabel != null)
                nameLabel.text = item.itemName;

            // Lock icon
            GameObject lockIcon = cell.transform.Find("LockIcon")?.gameObject;
            if (lockIcon != null)
                lockIcon.SetActive(!isUnlocked);

            // Equipped badge
            GameObject equippedBadge = cell.transform.Find("EquippedBadge")?.gameObject;
            if (equippedBadge != null)
                equippedBadge.SetActive(isEquipped);

            // Click handler
            Button btn = cell.GetComponent<Button>();
            if (btn != null)
            {
                CosmeticItem capturedItem = item;
                bool capturedUnlocked     = isUnlocked;
                btn.onClick.AddListener(() => OnItemCellClicked(capturedItem, capturedUnlocked));
            }
        }

        private void OnItemCellClicked(CosmeticItem item, bool isUnlocked)
        {
            if (!isUnlocked)
            {
                // Show unlock condition
                if (unlockConditionText != null)
                {
                    string condition = string.IsNullOrEmpty(item.unlockedByAchievement)
                        ? "Complete special requirements to unlock."
                        : $"Unlock requirement: {item.unlockedByAchievement}";
                    unlockConditionText.text = condition;
                }
                return;
            }

            if (unlockConditionText != null)
                unlockConditionText.text = string.Empty;

            // Equip immediately
            EquipItem(item);

            // Refresh grid badges
            SwitchTab(activeTab);
        }

        private void EquipItem(CosmeticItem item)
        {
            if (cosmeticSystem == null) return;

            equippedIdPerTab[item.itemType] = item.GetId();

            switch (item.itemType)
            {
                case CosmeticType.Suit:
                    cosmeticSystem.ApplySuit(item);
                    pendingLoadout.suitId = item.GetId();
                    break;
                case CosmeticType.GrappleSkin:
                    cosmeticSystem.ApplyGrappleSkin(item);
                    pendingLoadout.grappleSkinId = item.GetId();
                    break;
                case CosmeticType.RopeColor:
                    cosmeticSystem.ApplyRopeColor(item);
                    pendingLoadout.ropeColorId = item.GetId();
                    break;
                case CosmeticType.ParticleTrail:
                    cosmeticSystem.ApplyParticleTrail(item);
                    pendingLoadout.particleTrailId = item.GetId();
                    break;
            }

            // Update preview
            ShowPreview(item);
        }

        private void ShowPreview(CosmeticItem item)
        {
            if (previewRenderTexture != null && previewRenderTexture.gameObject.activeInHierarchy)
            {
                // Render texture camera handles 3D preview externally;
                // nothing to do here beyond keeping the RawImage visible.
                previewRenderTexture.enabled = true;
                if (previewSpriteImage != null)
                    previewSpriteImage.enabled = false;
            }
            else if (previewSpriteImage != null)
            {
                previewSpriteImage.enabled = item.previewSprite != null;
                previewSpriteImage.sprite  = item.previewSprite;
            }
        }

        // ── Save ──────────────────────────────────────────────────────────────────

        private void OnSaveLoadoutClicked()
        {
            if (cosmeticSystem != null)
                cosmeticSystem.SaveLoadout(pendingLoadout);
        }

        // ── Utility ───────────────────────────────────────────────────────────────

        private void ClearGrid()
        {
            foreach (GameObject cell in spawnedCells)
            {
                if (cell != null)
                    Destroy(cell);
            }
            spawnedCells.Clear();
        }
    }
}
