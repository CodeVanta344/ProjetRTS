using System.Collections.Generic;
using NapoleonicWars.Data;

namespace NapoleonicWars.Campaign
{
    /// <summary>
    /// Types of diplomatic actions available between factions
    /// </summary>
    public enum DiplomaticActionType
    {
        DeclareWar,
        ProposePeace,
        ProposeAlliance,
        BreakAlliance,
        FormAlliance,
        RequestMilitaryAccess,
        ProposeTradeAgreement,
        DemandVassalization,
        BecomeVassal,
        AcceptPeace,
        RejectOffer,
        ProposeMarriage
    }

    /// <summary>
    /// Types of alliances
    /// </summary>
    public enum AllianceType
    {
        Defensive,  // Only join if ally is attacked
        Offensive,  // Join ally's wars of aggression
        Full        // Both defensive and offensive
    }

    /// <summary>
    /// Types of treaties
    /// </summary>
    public enum TreatyType
    {
        Alliance,
        MilitaryAccess,
        TradeAgreement,
        Vassalage,
        NonAggression,
        Marriage
    }

    /// <summary>
    /// Represents a diplomatic offer from one faction to another
    /// </summary>
    [System.Serializable]
    public class DiplomaticOffer
    {
        public string offerId;
        public FactionType fromFaction;
        public FactionType toFaction;
        public DiplomaticActionType actionType;
        
        // For alliances
        public AllianceType allianceType;
        
        // For peace treaties
        public PeaceTerms peaceTerms;
        
        // For trade/tribute
        public float goldAmount;
        
        // For province transfers
        public List<string> provinceIds = new List<string>();
        
        // Timing
        public int turnProposed;
        public int expiresInTurns = 3;

        public bool IsExpired(int currentTurn)
        {
            return currentTurn - turnProposed >= expiresInTurns;
        }
    }

    /// <summary>
    /// Terms for a peace treaty
    /// </summary>
    [System.Serializable]
    public class PeaceTerms
    {
        public FactionType payingFaction;
        public FactionType receivingFaction;
        public float goldPayment;
        public List<string> provincesToCede = new List<string>();
        public bool releaseVassals;
        public bool breakAlliances;

        public PeaceTerms() { }

        public PeaceTerms(FactionType payer, FactionType receiver)
        {
            payingFaction = payer;
            receivingFaction = receiver;
        }

        /// <summary>
        /// Calculate the total value of these peace terms
        /// </summary>
        public float CalculateValue()
        {
            float value = goldPayment;
            value += provincesToCede.Count * 500f; // Each province worth ~500 gold
            return value;
        }
    }

    /// <summary>
    /// Represents an active treaty between factions
    /// </summary>
    [System.Serializable]
    public class Treaty
    {
        public string treatyId;
        public TreatyType type;
        public List<FactionType> factions = new List<FactionType>();
        public int turnSigned;
        public int duration = -1; // -1 = permanent until broken
        
        // Alliance specific
        public AllianceType allianceType;
        
        // Trade specific
        public float goldPerTurn;
        
        // Vassalage specific
        public FactionType overlord;
        public FactionType vassal;
        
        // Marriage specific
        public string character1Id;
        public string character2Id;

        public bool IsExpired(int currentTurn)
        {
            if (duration < 0) return false;
            return currentTurn - turnSigned >= duration;
        }
    }

    /// <summary>
    /// Historical record of diplomatic actions
    /// </summary>
    [System.Serializable]
    public class DiplomaticHistoryEntry
    {
        public int turn;
        public FactionType fromFaction;
        public FactionType toFaction;
        public DiplomaticActionType action;
        public string description;
    }

    /// <summary>
    /// Reasons why a diplomatic action might fail or be rejected
    /// </summary>
    public enum DiplomacyFailReason
    {
        None,
        AlreadyAtWar,
        AlreadyAllied,
        InsufficientGold,
        TreatyExists,
        RelationsTooLow,
        AtWarWithAlly,
        TargetIsVassal,
        NotAtWar
    }

    /// <summary>
    /// Result of a diplomatic action attempt
    /// </summary>
    public class DiplomacyResult
    {
        public bool success;
        public DiplomacyFailReason failReason;
        public string message;

        public static DiplomacyResult Success(string msg = "")
        {
            return new DiplomacyResult { success = true, message = msg };
        }

        public static DiplomacyResult Failure(DiplomacyFailReason reason, string msg = "")
        {
            return new DiplomacyResult { success = false, failReason = reason, message = msg };
        }
    }
}
