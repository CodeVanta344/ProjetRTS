using System.Collections.Generic;
using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Trade system managing trade routes, agreements, and resource exchange.
    /// </summary>
    public class TradeSystem
    {
        private static TradeSystem _instance;
        public static TradeSystem Instance => _instance ??= new TradeSystem();

        private Dictionary<string, TradeRoute> tradeRoutes = new Dictionary<string, TradeRoute>();
        private Dictionary<string, TradeAgreement> tradeAgreements = new Dictionary<string, TradeAgreement>();
        private Dictionary<string, TradePost> tradePosts = new Dictionary<string, TradePost>();

        // Market prices (fluctuate based on supply/demand)
        private Dictionary<ResourceType, float> marketPrices = new Dictionary<ResourceType, float>();

        public Dictionary<string, TradeRoute> TradeRoutes => tradeRoutes;
        public Dictionary<string, TradeAgreement> TradeAgreements => tradeAgreements;

        public void Initialize()
        {
            InitializeMarketPrices();
            CreateInitialTradePosts();
        }

        private void InitializeMarketPrices()
        {
            marketPrices[ResourceType.Gold] = 1f;
            marketPrices[ResourceType.Food] = 0.5f;
            marketPrices[ResourceType.Iron] = 2f;
            marketPrices[ResourceType.Wood] = 0.8f;
            marketPrices[ResourceType.Horses] = 5f;
            marketPrices[ResourceType.Ammunition] = 3f;
            marketPrices[ResourceType.Textiles] = 1.5f;
        }

        private void CreateInitialTradePosts()
        {
            // Major trade centers
            AddTradePost("london_trade", "London Exchange", "london", FactionType.Britain, 3);
            AddTradePost("paris_trade", "Paris Bourse", "paris", FactionType.France, 3);
            AddTradePost("amsterdam_trade", "Amsterdam Exchange", "rhineland", FactionType.Prussia, 2);
            AddTradePost("vienna_trade", "Vienna Market", "vienna", FactionType.Austria, 2);
            AddTradePost("constantinople_trade", "Grand Bazaar", "constantinople", FactionType.Ottoman, 3);
            AddTradePost("cadiz_trade", "Casa de Contratación", "andalusia", FactionType.Spain, 2);
        }

        private void AddTradePost(string id, string name, string provinceId, FactionType owner, int level)
        {
            tradePosts[id] = new TradePost
            {
                tradePostId = id,
                name = name,
                provinceId = provinceId,
                owner = owner,
                level = level
            };
        }

        #region Trade Routes

        public TradeRoute CreateTradeRoute(string fromCityId, string toCityId, ResourceType resource, float amount)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return null;

            if (!cm.Cities.TryGetValue(fromCityId, out var fromCity)) return null;
            if (!cm.Cities.TryGetValue(toCityId, out var toCity)) return null;

            // Check if route already exists
            string routeKey = GetRouteKey(fromCityId, toCityId);
            if (tradeRoutes.ContainsKey(routeKey))
                return null;

            // Check diplomatic relations
            var diplomacy = DiplomacySystem.Instance;
            if (diplomacy != null)
            {
                var state = diplomacy.GetRelationState(fromCity.owner, toCity.owner);
                if (state == DiplomacyState.War || state == DiplomacyState.Hostile)
                    return null;
            }

            var route = new TradeRoute
            {
                routeId = routeKey,
                fromCityId = fromCityId,
                toCityId = toCityId,
                fromFaction = fromCity.owner,
                toFaction = toCity.owner,
                resource = resource,
                amountPerTurn = amount,
                isActive = true
            };

            // Calculate route value
            route.goldPerTurn = CalculateRouteValue(route);

            tradeRoutes[routeKey] = route;
            Debug.Log($"[Trade] Route created: {fromCity.cityName} -> {toCity.cityName} ({resource})");

            return route;
        }

        public void CancelTradeRoute(string routeId)
        {
            if (tradeRoutes.ContainsKey(routeId))
            {
                tradeRoutes.Remove(routeId);
                Debug.Log($"[Trade] Route cancelled: {routeId}");
            }
        }

        private float CalculateRouteValue(TradeRoute route)
        {
            float baseValue = route.amountPerTurn * marketPrices.GetValueOrDefault(route.resource, 1f);

            // Distance modifier (longer routes = more profit but more risk)
            var cm = CampaignManager.Instance;
            if (cm != null && cm.Cities.TryGetValue(route.fromCityId, out var from) &&
                cm.Cities.TryGetValue(route.toCityId, out var to))
            {
                float distance = Vector2.Distance(from.mapPosition, to.mapPosition);
                baseValue *= 1f + distance * 0.5f;
            }

            return baseValue;
        }

        private string GetRouteKey(string from, string to)
        {
            return from.CompareTo(to) < 0 ? $"{from}_{to}" : $"{to}_{from}";
        }

        #endregion

        #region Trade Agreements

        public TradeAgreement CreateTradeAgreement(FactionType faction1, FactionType faction2)
        {
            string key = GetAgreementKey(faction1, faction2);
            if (tradeAgreements.ContainsKey(key))
                return tradeAgreements[key];

            var agreement = new TradeAgreement
            {
                agreementId = key,
                faction1 = faction1,
                faction2 = faction2,
                turnSigned = CampaignManager.Instance?.CurrentTurn ?? 0,
                isActive = true
            };

            // Calculate trade value based on faction economies
            agreement.goldPerTurnFaction1 = CalculateAgreementValue(faction1, faction2);
            agreement.goldPerTurnFaction2 = CalculateAgreementValue(faction2, faction1);

            tradeAgreements[key] = agreement;
            Debug.Log($"[Trade] Agreement signed: {faction1} <-> {faction2}");

            return agreement;
        }

        public void CancelTradeAgreement(FactionType faction1, FactionType faction2)
        {
            string key = GetAgreementKey(faction1, faction2);
            if (tradeAgreements.ContainsKey(key))
            {
                tradeAgreements.Remove(key);
                Debug.Log($"[Trade] Agreement cancelled: {faction1} <-> {faction2}");
            }
        }

        private float CalculateAgreementValue(FactionType exporter, FactionType importer)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return 10f;

            // Base value from province count
            int exporterProvinces = cm.Factions[exporter].ownedProvinceIds.Count;
            int importerProvinces = cm.Factions[importer].ownedProvinceIds.Count;

            float value = (exporterProvinces + importerProvinces) * 5f;

            // Bonus for trade posts
            foreach (var post in tradePosts.Values)
            {
                if (post.owner == exporter || post.owner == importer)
                    value += post.level * 10f;
            }

            return value;
        }

        private string GetAgreementKey(FactionType f1, FactionType f2)
        {
            return f1 < f2 ? $"{f1}_{f2}" : $"{f2}_{f1}";
        }

        public bool HasTradeAgreement(FactionType faction1, FactionType faction2)
        {
            string key = GetAgreementKey(faction1, faction2);
            return tradeAgreements.ContainsKey(key) && tradeAgreements[key].isActive;
        }

        #endregion

        #region Turn Processing

        public void ProcessTurn()
        {
            ProcessTradeRoutes();
            ProcessTradeAgreements();
            UpdateMarketPrices();
            ProcessPiracy();
        }

        private void ProcessTradeRoutes()
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            var routesToCancel = new List<string>();

            foreach (var route in tradeRoutes.Values)
            {
                if (!route.isActive) continue;

                // Check if route is still valid
                if (!IsRouteValid(route))
                {
                    routesToCancel.Add(route.routeId);
                    continue;
                }

                // Transfer resources and gold
                if (cm.Cities.TryGetValue(route.fromCityId, out var fromCity) &&
                    cm.Cities.TryGetValue(route.toCityId, out var toCity))
                {
                    // Exporter gets gold
                    cm.Factions[fromCity.owner].gold += route.goldPerTurn;
                    
                    // Importer gets resources (simplified)
                    cm.Factions[toCity.owner].gold += route.goldPerTurn * 0.5f;
                }
            }

            foreach (var routeId in routesToCancel)
            {
                CancelTradeRoute(routeId);
            }
        }

        private void ProcessTradeAgreements()
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return;

            var agreementsToCancel = new List<string>();

            foreach (var agreement in tradeAgreements.Values)
            {
                if (!agreement.isActive) continue;

                // Check if still valid (not at war)
                var diplomacy = DiplomacySystem.Instance;
                if (diplomacy != null)
                {
                    var state = diplomacy.GetRelationState(agreement.faction1, agreement.faction2);
                    if (state == DiplomacyState.War)
                    {
                        agreementsToCancel.Add(agreement.agreementId);
                        continue;
                    }
                }

                // Apply trade income
                cm.Factions[agreement.faction1].gold += agreement.goldPerTurnFaction1;
                cm.Factions[agreement.faction2].gold += agreement.goldPerTurnFaction2;
            }

            foreach (var agreementId in agreementsToCancel)
            {
                var agreement = tradeAgreements[agreementId];
                CancelTradeAgreement(agreement.faction1, agreement.faction2);
            }
        }

        private bool IsRouteValid(TradeRoute route)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return false;

            // Check cities still exist and are owned by original factions
            if (!cm.Cities.TryGetValue(route.fromCityId, out var fromCity)) return false;
            if (!cm.Cities.TryGetValue(route.toCityId, out var toCity)) return false;

            if (fromCity.owner != route.fromFaction) return false;
            if (toCity.owner != route.toFaction) return false;

            // Check not at war
            var diplomacy = DiplomacySystem.Instance;
            if (diplomacy != null)
            {
                var state = diplomacy.GetRelationState(route.fromFaction, route.toFaction);
                if (state == DiplomacyState.War) return false;
            }

            return true;
        }

        private void UpdateMarketPrices()
        {
            // Fluctuate prices slightly each turn
            foreach (var resource in new List<ResourceType>(marketPrices.Keys))
            {
                float change = Random.Range(-0.1f, 0.1f);
                float newPrice = marketPrices[resource] * (1f + change);
                marketPrices[resource] = Mathf.Clamp(newPrice, 0.1f, 10f);
            }
        }

        private void ProcessPiracy()
        {
            // Naval blockades and piracy affect trade routes
            var naval = NavalCampaignManager.Instance;
            if (naval == null) return;

            foreach (var route in tradeRoutes.Values)
            {
                if (!route.isActive) continue;
                if (!route.isSeaRoute) continue;

                // Check for blockades along route
                // (simplified - would need proper sea zone system)
                if (Random.value < 0.05f) // 5% chance of piracy
                {
                    route.goldPerTurn *= 0.8f; // 20% loss
                    Debug.Log($"[Trade] Piracy affected route {route.routeId}!");
                }
            }
        }

        #endregion

        #region Market

        public float GetMarketPrice(ResourceType resource)
        {
            return marketPrices.GetValueOrDefault(resource, 1f);
        }

        public bool BuyResource(FactionType faction, ResourceType resource, float amount)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return false;

            float cost = amount * GetMarketPrice(resource);
            
            if (!cm.Factions[faction].CanAfford(cost))
                return false;

            cm.Factions[faction].gold -= cost;

            switch (resource)
            {
                case ResourceType.Food:
                    cm.Factions[faction].food += amount;
                    break;
                case ResourceType.Iron:
                    cm.Factions[faction].iron += amount;
                    break;
            }

            // Buying increases price
            marketPrices[resource] *= 1.02f;

            return true;
        }

        public bool SellResource(FactionType faction, ResourceType resource, float amount)
        {
            var cm = CampaignManager.Instance;
            if (cm == null) return false;

            // Check if faction has the resource
            float available = 0f;
            switch (resource)
            {
                case ResourceType.Food:
                    available = cm.Factions[faction].food;
                    break;
                case ResourceType.Iron:
                    available = cm.Factions[faction].iron;
                    break;
            }

            if (available < amount)
                return false;

            float revenue = amount * GetMarketPrice(resource) * 0.9f; // 10% market fee

            switch (resource)
            {
                case ResourceType.Food:
                    cm.Factions[faction].food -= amount;
                    break;
                case ResourceType.Iron:
                    cm.Factions[faction].iron -= amount;
                    break;
            }

            cm.Factions[faction].gold += revenue;

            // Selling decreases price
            marketPrices[resource] *= 0.98f;

            return true;
        }

        #endregion
    }

    #region Data Classes

    [System.Serializable]
    public class TradeRoute
    {
        public string routeId;
        public string fromCityId;
        public string toCityId;
        public FactionType fromFaction;
        public FactionType toFaction;
        public ResourceType resource;
        public float amountPerTurn;
        public float goldPerTurn;
        public bool isActive;
        public bool isSeaRoute;
    }

    [System.Serializable]
    public class TradeAgreement
    {
        public string agreementId;
        public FactionType faction1;
        public FactionType faction2;
        public float goldPerTurnFaction1;
        public float goldPerTurnFaction2;
        public int turnSigned;
        public bool isActive;
    }

    [System.Serializable]
    public class TradePost
    {
        public string tradePostId;
        public string name;
        public string provinceId;
        public FactionType owner;
        public int level; // 1-3
        public float bonusIncome => level * 25f;
    }

    // ResourceType is defined in ProductionChain.cs

    #endregion
}
