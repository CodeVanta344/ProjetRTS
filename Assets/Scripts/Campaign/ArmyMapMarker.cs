using UnityEngine;
using NapoleonicWars.Campaign;
using NapoleonicWars.Data;
using System.Collections.Generic;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Army marker for the campaign map.
    /// Displays a base token with a flagpole/banner AND procedural 3D proxy
    /// figures (infantry in formation, cavalry, cannons) based on army composition.
    /// </summary>
    public class ArmyMapMarker : MonoBehaviour
    {
        public ArmyData ArmyData { get; private set; }
        public CampaignMap3D Map { get; private set; }
        
        private GameObject selectionRing;
        private bool isSelected;
        private GameObject proxyRoot; // Root for 3D proxy figures
        
        // Events
        public System.Action<ArmyData> OnArmySelected;
        public System.Action<ArmyData> OnArmyHoverStart;
        public System.Action OnArmyHoverEnd;
        
        // Scale — visible but not overwhelming on a 10,000-unit map
        private const float S = 18f;
        
        // Proxy figure sizes
        private const float FIG = 2.2f;   // Base figure height multiplier
        
        public void Initialize(ArmyData army, CampaignMap3D map)
        {
            ArmyData = army;
            Map = map;
            
            Color factionCol = GetFactionColor(army.faction);
            Color darkFaction = factionCol * 0.5f;
            darkFaction.a = 1f;
            
            // ──── BASE TOKEN: flat thin cylinder ────
            var baseToken = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseToken.name = "Token";
            baseToken.transform.SetParent(transform);
            baseToken.transform.localPosition = Vector3.zero;
            baseToken.transform.localScale = new Vector3(S * 1.6f, S * 0.08f, S * 1.6f);
            SetColor(baseToken, factionCol);
            DestroyImmediate(baseToken.GetComponent<Collider>());
            
            // ──── INNER RING: darker rim for depth ────
            var innerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            innerRing.name = "InnerRing";
            innerRing.transform.SetParent(transform);
            innerRing.transform.localPosition = new Vector3(0f, S * 0.01f, 0f);
            innerRing.transform.localScale = new Vector3(S * 1.2f, S * 0.1f, S * 1.2f);
            SetColor(innerRing, darkFaction);
            DestroyImmediate(innerRing.GetComponent<Collider>());
            
            // ──── FLAGPOLE: thin vertical stick ────
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(transform);
            pole.transform.localPosition = new Vector3(0f, S * 1.2f, 0f);
            pole.transform.localScale = new Vector3(S * 0.05f, S * 1.2f, S * 0.05f);
            SetColor(pole, new Color(0.25f, 0.18f, 0.12f)); // dark wood
            DestroyImmediate(pole.GetComponent<Collider>());
            
            // ──── FLAG BANNER: small rectangle ────
            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "Flag";
            flag.transform.SetParent(transform);
            flag.transform.localPosition = new Vector3(S * 0.35f, S * 2.1f, 0f);
            flag.transform.localScale = new Vector3(S * 0.6f, S * 0.4f, S * 0.06f);
            SetColor(flag, factionCol);
            DestroyImmediate(flag.GetComponent<Collider>());
            
            // ──── FLAG STRIPE: decorative stripe on flag ────
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Stripe";
            stripe.transform.SetParent(transform);
            stripe.transform.localPosition = new Vector3(S * 0.35f, S * 2.1f, S * 0.035f);
            stripe.transform.localScale = new Vector3(S * 0.55f, S * 0.12f, S * 0.01f);
            SetColor(stripe, Color.Lerp(factionCol, Color.white, 0.7f));
            DestroyImmediate(stripe.GetComponent<Collider>());
            
            // ──── 3D PROXY FIGURES ────
            BuildProxyFigures(factionCol);
            
            // ──── COLLIDER for clicking ────
            var col = gameObject.AddComponent<SphereCollider>();
            col.radius = S * 1.5f;
            col.center = new Vector3(0f, S * 0.5f, 0f);
            
            // ──── Selection ring (hidden) ────
            selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            selectionRing.name = "Selection";
            selectionRing.transform.SetParent(transform);
            selectionRing.transform.localPosition = Vector3.zero;
            selectionRing.transform.localScale = new Vector3(S * 2.0f, S * 0.04f, S * 2.0f);
            SetColor(selectionRing, new Color(1f, 1f, 0.3f));
            DestroyImmediate(selectionRing.GetComponent<Collider>());
            selectionRing.SetActive(false);
        }
        
        // ═════════════════════════════════════════════════════════════
        //  3D PROXY FIGURE SYSTEM
        // ═════════════════════════════════════════════════════════════
        
        private void BuildProxyFigures(Color factionCol)
        {
            if (proxyRoot != null) Destroy(proxyRoot);
            
            proxyRoot = new GameObject("ProxyFigures");
            proxyRoot.transform.SetParent(transform);
            proxyRoot.transform.localPosition = new Vector3(0f, S * 0.15f, 0f);
            proxyRoot.transform.localRotation = Quaternion.identity;
            
            if (ArmyData == null || ArmyData.regiments.Count == 0) return;
            
            // Count regiment types
            int infCount = 0, cavCount = 0, artCount = 0;
            foreach (var reg in ArmyData.regiments)
            {
                if (IsInfantry(reg.unitType)) infCount++;
                else if (IsCavalry(reg.unitType)) cavCount++;
                else if (IsArtillery(reg.unitType)) artCount++;
            }
            
            int total = infCount + cavCount + artCount;
            if (total == 0) return;
            
            // Determine dominant type and how many figures to show
            // Max ~8 proxy figures to keep it clean
            int maxFigures = Mathf.Min(total, 8);
            int infFigs = Mathf.RoundToInt((float)infCount / total * maxFigures);
            int cavFigs = Mathf.RoundToInt((float)cavCount / total * maxFigures);
            int artFigs = Mathf.RoundToInt((float)artCount / total * maxFigures);
            
            // Ensure at least 1 figure for each present type
            if (infCount > 0 && infFigs == 0) infFigs = 1;
            if (cavCount > 0 && cavFigs == 0) cavFigs = 1;
            if (artCount > 0 && artFigs == 0) artFigs = 1;
            
            // Clamp total to maxFigures
            int totalFigs = infFigs + cavFigs + artFigs;
            while (totalFigs > maxFigures)
            {
                // Reduce the largest group
                if (infFigs >= cavFigs && infFigs >= artFigs && infFigs > 1) infFigs--;
                else if (cavFigs >= artFigs && cavFigs > 1) cavFigs--;
                else if (artFigs > 1) artFigs--;
                else break;
                totalFigs = infFigs + cavFigs + artFigs;
            }
            
            // Layout: arrange figures in a grid on the token
            float spacing = S * 0.45f;
            int idx = 0;
            
            // Place infantry (front rows)
            for (int i = 0; i < infFigs; i++)
            {
                Vector3 pos = GetFigurePosition(idx, totalFigs, spacing);
                BuildInfantryFigure(proxyRoot.transform, pos, factionCol);
                idx++;
            }
            
            // Place cavalry (middle)
            for (int i = 0; i < cavFigs; i++)
            {
                Vector3 pos = GetFigurePosition(idx, totalFigs, spacing);
                BuildCavalryFigure(proxyRoot.transform, pos, factionCol);
                idx++;
            }
            
            // Place artillery (rear)
            for (int i = 0; i < artFigs; i++)
            {
                Vector3 pos = GetFigurePosition(idx, totalFigs, spacing);
                BuildArtilleryFigure(proxyRoot.transform, pos, factionCol);
                idx++;
            }
        }
        
        /// <summary>Grid layout position for figure index</summary>
        private Vector3 GetFigurePosition(int index, int total, float spacing)
        {
            // Arrange in rows of 4, centered on the token
            int cols = Mathf.Min(total, 4);
            int row = index / cols;
            int col = index % cols;
            
            float rowOffset = (total > 4) ? 2 : 1;
            float colOffset = (cols - 1) * 0.5f;
            
            float x = (col - colOffset) * spacing;
            float z = (row - (rowOffset - 1) * 0.5f) * spacing * 0.9f;
            
            return new Vector3(x, 0f, z);
        }
        
        // ─────────────── INFANTRY FIGURE ───────────────
        // Small capsule body + tiny sphere head
        private void BuildInfantryFigure(Transform parent, Vector3 pos, Color factionCol)
        {
            GameObject fig = new GameObject("InfFig");
            fig.transform.SetParent(parent);
            fig.transform.localPosition = pos;
            fig.transform.localRotation = Quaternion.identity;
            
            // Body (capsule)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(fig.transform);
            body.transform.localPosition = new Vector3(0f, FIG * 1.5f, 0f);
            body.transform.localScale = new Vector3(FIG * 0.5f, FIG * 1.2f, FIG * 0.5f);
            SetColor(body, factionCol);
            DestroyImmediate(body.GetComponent<Collider>());
            
            // Head (sphere)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(fig.transform);
            head.transform.localPosition = new Vector3(0f, FIG * 3.0f, 0f);
            head.transform.localScale = new Vector3(FIG * 0.45f, FIG * 0.55f, FIG * 0.45f);
            SetColor(head, new Color(0.85f, 0.72f, 0.60f)); // Skin tone
            DestroyImmediate(head.GetComponent<Collider>());
            
            // Shako hat (small cylinder on top)
            var hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hat.name = "Shako";
            hat.transform.SetParent(fig.transform);
            hat.transform.localPosition = new Vector3(0f, FIG * 3.6f, 0f);
            hat.transform.localScale = new Vector3(FIG * 0.35f, FIG * 0.35f, FIG * 0.35f);
            SetColor(hat, new Color(0.12f, 0.12f, 0.15f)); // Dark shako
            DestroyImmediate(hat.GetComponent<Collider>());
            
            // Musket (thin cube, angled)
            var musket = GameObject.CreatePrimitive(PrimitiveType.Cube);
            musket.name = "Musket";
            musket.transform.SetParent(fig.transform);
            musket.transform.localPosition = new Vector3(FIG * 0.35f, FIG * 2.2f, 0f);
            musket.transform.localScale = new Vector3(FIG * 0.08f, FIG * 2.5f, FIG * 0.08f);
            musket.transform.localRotation = Quaternion.Euler(0f, 0f, -8f); // Slight tilt
            SetColor(musket, new Color(0.32f, 0.22f, 0.14f)); // Wood brown
            DestroyImmediate(musket.GetComponent<Collider>());
        }
        
        // ─────────────── CAVALRY FIGURE ───────────────
        // Horse body (elongated cube) + rider (capsule) on top
        private void BuildCavalryFigure(Transform parent, Vector3 pos, Color factionCol)
        {
            GameObject fig = new GameObject("CavFig");
            fig.transform.SetParent(parent);
            fig.transform.localPosition = pos;
            fig.transform.localRotation = Quaternion.identity;
            
            // Horse body (elongated cube)
            var horse = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horse.name = "Horse";
            horse.transform.SetParent(fig.transform);
            horse.transform.localPosition = new Vector3(0f, FIG * 1.3f, 0f);
            horse.transform.localScale = new Vector3(FIG * 0.45f, FIG * 0.7f, FIG * 1.2f);
            SetColor(horse, new Color(0.40f, 0.28f, 0.16f)); // Brown horse
            DestroyImmediate(horse.GetComponent<Collider>());
            
            // Horse head (small cube, forward)
            var horseHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horseHead.name = "HorseHead";
            horseHead.transform.SetParent(fig.transform);
            horseHead.transform.localPosition = new Vector3(0f, FIG * 1.8f, FIG * 0.8f);
            horseHead.transform.localScale = new Vector3(FIG * 0.25f, FIG * 0.35f, FIG * 0.45f);
            horseHead.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            SetColor(horseHead, new Color(0.35f, 0.25f, 0.14f));
            DestroyImmediate(horseHead.GetComponent<Collider>());
            
            // Horse legs (4 thin cubes)
            float[] legX = {-FIG * 0.15f, FIG * 0.15f, -FIG * 0.15f, FIG * 0.15f};
            float[] legZ = {-FIG * 0.35f, -FIG * 0.35f, FIG * 0.35f, FIG * 0.35f};
            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Leg{i}";
                leg.transform.SetParent(fig.transform);
                leg.transform.localPosition = new Vector3(legX[i], FIG * 0.45f, legZ[i]);
                leg.transform.localScale = new Vector3(FIG * 0.12f, FIG * 0.9f, FIG * 0.12f);
                SetColor(leg, new Color(0.38f, 0.26f, 0.15f));
                DestroyImmediate(leg.GetComponent<Collider>());
            }
            
            // Rider body (capsule on top of horse)
            var rider = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rider.name = "Rider";
            rider.transform.SetParent(fig.transform);
            rider.transform.localPosition = new Vector3(0f, FIG * 2.4f, 0f);
            rider.transform.localScale = new Vector3(FIG * 0.35f, FIG * 0.7f, FIG * 0.35f);
            SetColor(rider, factionCol);
            DestroyImmediate(rider.GetComponent<Collider>());
            
            // Rider head
            var rHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rHead.name = "RiderHead";
            rHead.transform.SetParent(fig.transform);
            rHead.transform.localPosition = new Vector3(0f, FIG * 3.3f, 0f);
            rHead.transform.localScale = new Vector3(FIG * 0.35f, FIG * 0.4f, FIG * 0.35f);
            SetColor(rHead, new Color(0.85f, 0.72f, 0.60f));
            DestroyImmediate(rHead.GetComponent<Collider>());
            
            // Sabre (thin angled cube)
            var sabre = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sabre.name = "Sabre";
            sabre.transform.SetParent(fig.transform);
            sabre.transform.localPosition = new Vector3(FIG * 0.35f, FIG * 2.8f, FIG * 0.2f);
            sabre.transform.localScale = new Vector3(FIG * 0.06f, FIG * 1.0f, FIG * 0.06f);
            sabre.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            SetColor(sabre, new Color(0.75f, 0.75f, 0.80f)); // Steel
            DestroyImmediate(sabre.GetComponent<Collider>());
        }
        
        // ─────────────── ARTILLERY FIGURE ───────────────
        // Cannon barrel (cylinder) on a carriage (cube) with wheels
        private void BuildArtilleryFigure(Transform parent, Vector3 pos, Color factionCol)
        {
            GameObject fig = new GameObject("ArtFig");
            fig.transform.SetParent(parent);
            fig.transform.localPosition = pos;
            fig.transform.localRotation = Quaternion.identity;
            
            // Carriage (base cube)
            var carriage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carriage.name = "Carriage";
            carriage.transform.SetParent(fig.transform);
            carriage.transform.localPosition = new Vector3(0f, FIG * 0.5f, 0f);
            carriage.transform.localScale = new Vector3(FIG * 0.6f, FIG * 0.25f, FIG * 1.0f);
            SetColor(carriage, new Color(0.30f, 0.22f, 0.12f)); // Wood
            DestroyImmediate(carriage.GetComponent<Collider>());
            
            // Cannon barrel (cylinder, horizontal)
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            barrel.transform.SetParent(fig.transform);
            barrel.transform.localPosition = new Vector3(0f, FIG * 0.9f, FIG * 0.3f);
            barrel.transform.localScale = new Vector3(FIG * 0.2f, FIG * 0.6f, FIG * 0.2f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Horizontal
            SetColor(barrel, new Color(0.25f, 0.25f, 0.28f)); // Dark iron
            DestroyImmediate(barrel.GetComponent<Collider>());
            
            // Wheels (2 thin cylinders on sides)
            for (int side = -1; side <= 1; side += 2)
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"Wheel{(side > 0 ? "R" : "L")}";
                wheel.transform.SetParent(fig.transform);
                wheel.transform.localPosition = new Vector3(FIG * 0.4f * side, FIG * 0.5f, 0f);
                wheel.transform.localScale = new Vector3(FIG * 0.5f, FIG * 0.06f, FIG * 0.5f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                SetColor(wheel, new Color(0.25f, 0.18f, 0.10f)); // Dark wood
                DestroyImmediate(wheel.GetComponent<Collider>());
            }
            
            // Trail/limber (thin cube extending backward)
            var trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trail.name = "Trail";
            trail.transform.SetParent(fig.transform);
            trail.transform.localPosition = new Vector3(0f, FIG * 0.35f, -FIG * 0.7f);
            trail.transform.localScale = new Vector3(FIG * 0.15f, FIG * 0.12f, FIG * 0.8f);
            SetColor(trail, new Color(0.28f, 0.20f, 0.12f));
            DestroyImmediate(trail.GetComponent<Collider>());
        }
        
        // ═════════════════════════════════════════════════════════════
        //  UNIT TYPE CLASSIFICATION
        // ═════════════════════════════════════════════════════════════
        
        private static bool IsInfantry(UnitType t)
        {
            switch (t)
            {
                case UnitType.Militia:
                case UnitType.TrainedMilitia:
                case UnitType.LineInfantry:
                case UnitType.LightInfantry:
                case UnitType.Fusilier:
                case UnitType.Grenadier:
                case UnitType.Voltigeur:
                case UnitType.Chasseur:
                case UnitType.GuardInfantry:
                case UnitType.OldGuard:
                case UnitType.Engineer:
                case UnitType.Sapper:
                case UnitType.Marine:
                case UnitType.Partisan:
                    return true;
                default: return false;
            }
        }
        
        private static bool IsCavalry(UnitType t)
        {
            switch (t)
            {
                case UnitType.MilitiaCavalry:
                case UnitType.Dragoon:
                case UnitType.Cavalry:
                case UnitType.Hussar:
                case UnitType.Lancer:
                case UnitType.Cuirassier:
                case UnitType.GuardCavalry:
                case UnitType.Mameluke:
                    return true;
                default: return false;
            }
        }
        
        private static bool IsArtillery(UnitType t)
        {
            switch (t)
            {
                case UnitType.GarrisonCannon:
                case UnitType.Artillery:
                case UnitType.HorseArtillery:
                case UnitType.Howitzer:
                case UnitType.GrandBattery:
                case UnitType.GuardArtillery:
                    return true;
                default: return false;
            }
        }
        
        // ═════════════════════════════════════════════════════════════
        //  REFRESH — call when army composition changes
        // ═════════════════════════════════════════════════════════════
        
        public void RefreshProxy()
        {
            if (ArmyData == null) return;
            Color factionCol = GetFactionColor(ArmyData.faction);
            BuildProxyFigures(factionCol);
        }
        
        // ═════════════════════════════════════════════════════════════
        //  UTILITIES
        // ═════════════════════════════════════════════════════════════
        
        private void SetColor(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            
            Material mat = null;
            string[] shaders = { "Universal Render Pipeline/Lit", "Standard", "Diffuse", "Unlit/Color" };
            foreach (var name in shaders)
            {
                var sh = Shader.Find(name);
                if (sh != null) { mat = new Material(sh); break; }
            }
            if (mat == null) mat = new Material(rend.material);
            
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
            rend.material = mat;
        }
        
        private Color GetFactionColor(FactionType f)
        {
            return f switch
            {
                FactionType.France  => new Color(0.20f, 0.35f, 0.75f),  // French blue
                FactionType.Britain => new Color(0.80f, 0.15f, 0.15f),  // British red
                FactionType.Prussia => new Color(0.15f, 0.15f, 0.20f),  // Prussian dark
                FactionType.Russia  => new Color(0.12f, 0.50f, 0.18f),  // Russian green
                FactionType.Austria => new Color(0.90f, 0.85f, 0.45f),  // Austrian gold
                FactionType.Spain   => new Color(0.85f, 0.50f, 0.12f),  // Spanish orange
                FactionType.Ottoman => new Color(0.70f, 0.15f, 0.18f),  // Ottoman crimson
                _ => Color.gray
            };
        }
        
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selectionRing != null) selectionRing.SetActive(selected);
        }
        
        public void SetHovered(bool hovered) { }
        
        public string GetTooltipText()
        {
            if (ArmyData == null) return "";
            string t = $"<b>{ArmyData.armyName}</b>\n{ArmyData.faction} — {ArmyData.TotalSoldiers} soldats\n";
            foreach (var r in ArmyData.regiments)
                t += $"  • {r.regimentName}: {r.currentSize}/{r.maxSize}\n";
            return t;
        }
        
        public string GetQuickSummary()
        {
            if (ArmyData == null) return "";
            return $"{ArmyData.regiments.Count} reg, {ArmyData.TotalSoldiers} h";
        }
        
        private void OnMouseEnter() { OnArmyHoverStart?.Invoke(ArmyData); }
        private void OnMouseExit()  { OnArmyHoverEnd?.Invoke(); }
        // OnMouseDown removed — selection managed by CampaignMap3D.HandleInput via raycast
        
        public void UpdatePosition(Vector3 pos)
        {
            transform.position = pos;
        }
        
        public void ShowMovementRange(string[] n) { }
    }
}
