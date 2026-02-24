using UnityEngine;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Visual marker for armies on the campaign map with click/interaction support.
    /// Allows players to select armies, view unit info, and issue move orders.
    /// </summary>
    public class ArmyMapMarker : MonoBehaviour
    {
        [Header("Army Data")]
        public ArmyData ArmyData { get; private set; }
        public CampaignMap3D Map { get; private set; }
        
        [Header("Visual Components")]
        private GameObject selectionRing;
        private GameObject hoverRing;
        private GameObject infoPanel;
        private Renderer markerRenderer;
        
        [Header("State")]
        private bool isSelected;
        private bool isHovered;
        
        // Events
        public System.Action<ArmyData> OnArmySelected;
        public System.Action<ArmyData> OnArmyHoverStart;
        public System.Action OnArmyHoverEnd;
        
        /// <summary>
        /// Initialize the army marker with data and visual setup
        /// </summary>
        public void Initialize(ArmyData army, CampaignMap3D map)
        {
            ArmyData = army;
            Map = map;
            
            // Create visual components
            CreateVisuals();
            CreateSelectionRing();
            CreateHoverRing();
            
            // Add collider for raycast detection
            SphereCollider collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = 1.5f;
            collider.isTrigger = true;
        }
        
        private void CreateVisuals()
        {
            // Main marker - capsule shape
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = "MarkerMesh";
            marker.transform.SetParent(transform);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(1.5f, 2.0f, 1.5f); // Scaled up for giant map
            
            markerRenderer = marker.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            mat.color = GetFactionColor(ArmyData.faction);
            markerRenderer.material = mat;
            
            Destroy(marker.GetComponent<Collider>());
            
            // Add small flag/icon above
            CreateUnitTypeIndicator();
        }
        
        private void CreateUnitTypeIndicator()
        {
            // Create a small flag/icon showing primary unit type
            GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "UnitFlag";
            flag.transform.SetParent(transform);
            flag.transform.localPosition = new Vector3(0f, 2.5f, 0f); // Higher up
            flag.transform.localScale = new Vector3(0.8f, 0.6f, 0.1f); // Larger flag
            
            Renderer flagRenderer = flag.GetComponent<Renderer>();
            Material flagMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            
            // Color based on primary unit type in army
            flagMat.color = GetPrimaryUnitTypeColor();
            flagRenderer.material = flagMat;
            
            Destroy(flag.GetComponent<Collider>());
        }
        
        private Color GetPrimaryUnitTypeColor()
        {
            if (ArmyData.regiments.Count == 0) return Color.gray;
            
            // Find most common unit type
            int infantry = 0, cavalry = 0, artillery = 0;
            foreach (var reg in ArmyData.regiments)
            {
                switch (reg.unitType)
                {
                    case UnitType.LineInfantry:
                    case UnitType.LightInfantry:
                    case UnitType.Grenadier:
                        infantry++;
                        break;
                    case UnitType.Cavalry:
                    case UnitType.Hussar:
                    case UnitType.Lancer:
                        cavalry++;
                        break;
                    case UnitType.Artillery:
                        artillery++;
                        break;
                }
            }
            
            if (artillery >= cavalry && artillery >= infantry)
                return new Color(0.4f, 0.4f, 0.4f); // Dark gray for artillery
            if (cavalry >= infantry)
                return new Color(0.8f, 0.6f, 0.2f); // Gold for cavalry
            return new Color(0.2f, 0.5f, 0.2f); // Green for infantry
        }
        
        private void CreateSelectionRing()
        {
            selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            selectionRing.name = "SelectionRing";
            selectionRing.transform.SetParent(transform);
            selectionRing.transform.localPosition = new Vector3(0f, -1.8f, 0f);
            selectionRing.transform.localScale = new Vector3(3.5f, 0.05f, 3.5f);
            
            Renderer ringRenderer = selectionRing.GetComponent<Renderer>();
            Material ringMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            ringMat.color = Color.yellow;
            ringRenderer.material = ringMat;
            
            Destroy(selectionRing.GetComponent<Collider>());
            selectionRing.SetActive(false);
        }
        
        private void CreateHoverRing()
        {
            hoverRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hoverRing.name = "HoverRing";
            hoverRing.transform.SetParent(transform);
            hoverRing.transform.localPosition = new Vector3(0f, -1.75f, 0f);
            hoverRing.transform.localScale = new Vector3(3.8f, 0.03f, 3.8f);
            
            Renderer ringRenderer = hoverRing.GetComponent<Renderer>();
            Material ringMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            ringMat.color = new Color(1f, 1f, 0.5f, 0.7f);
            ringRenderer.material = ringMat;
            
            Destroy(hoverRing.GetComponent<Collider>());
            hoverRing.SetActive(false);
        }
        
        private Color GetFactionColor(FactionType faction)
        {
            return faction switch
            {
                FactionType.France  => new Color(0.30f, 0.45f, 0.85f),
                FactionType.Britain => new Color(0.85f, 0.18f, 0.18f),
                FactionType.Prussia => new Color(0.20f, 0.20f, 0.25f),
                FactionType.Russia  => new Color(0.15f, 0.55f, 0.20f),
                FactionType.Austria => new Color(0.92f, 0.82f, 0.35f),
                FactionType.Spain   => new Color(0.90f, 0.55f, 0.15f),
                FactionType.Ottoman => new Color(0.55f, 0.15f, 0.20f),
                _ => Color.gray
            };
        }
        
        /// <summary>
        /// Set selection state and update visual
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selectionRing != null)
                selectionRing.SetActive(selected);
        }
        
        /// <summary>
        /// Set hover state and update visual
        /// </summary>
        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            if (hoverRing != null)
                hoverRing.SetActive(hovered);
        }
        
        /// <summary>
        /// Get formatted army info for tooltip display
        /// </summary>
        public string GetTooltipText()
        {
            if (ArmyData == null) return "";
            
            string text = $"<b>{ArmyData.armyName}</b>\n";
            text += $"Faction: {ArmyData.faction}\n";
            text += $"Total Soldiers: {ArmyData.TotalSoldiers}\n";
            text += $"Movement: {ArmyData.movementPoints}/{ArmyData.maxMovementPoints}\n\n";
            
            // Count by unit type
            text += "<b>Composition:</b>\n";
            foreach (var reg in ArmyData.regiments)
            {
                string typeIcon = reg.unitType switch
                {
                    UnitType.LineInfantry => "⚔",
                    UnitType.LightInfantry => "🏹",
                    UnitType.Grenadier => "💣",
                    UnitType.Cavalry => "🐎",
                    UnitType.Hussar => "⚡",
                    UnitType.Lancer => "🔱",
                    UnitType.Artillery => "💥",
                    _ => "⚔"
                };
                text += $"{typeIcon} {reg.regimentName}: {reg.currentSize}/{reg.maxSize}\n";
            }
            
            return text;
        }
        
        /// <summary>
        /// Get quick summary for UI display
        /// </summary>
        public string GetQuickSummary()
        {
            if (ArmyData == null) return "";
            
            int regiments = ArmyData.regiments.Count;
            int soldiers = ArmyData.TotalSoldiers;
            
            return $"{regiments} regiments, {soldiers} men";
        }
        
        // === Mouse Interaction ===
        
        private void OnMouseEnter()
        {
            SetHovered(true);
            OnArmyHoverStart?.Invoke(ArmyData);
        }
        
        private void OnMouseExit()
        {
            SetHovered(false);
            OnArmyHoverEnd?.Invoke();
        }
        
        private void OnMouseDown()
        {
            // Left click - select army
            OnArmySelected?.Invoke(ArmyData);
        }
        
        private void OnMouseOver()
        {
            // Continuous hover - could show tooltip
        }
        
        /// <summary>
        /// Update the army position on the map
        /// </summary>
        public void UpdatePosition(Vector3 worldPosition)
        {
            transform.position = Vector3.Lerp(transform.position, worldPosition, Time.deltaTime * 5f);
        }
        
        /// <summary>
        /// Show movement range indicator
        /// </summary>
        public void ShowMovementRange(string[] neighborProvinceIds)
        {
            // Could create visual lines to neighbor provinces
            // For now, handled at CampaignMap3D level
        }
        
        private void OnDestroy()
        {
            // Clean up any instantiated objects if needed
        }
    }
}
