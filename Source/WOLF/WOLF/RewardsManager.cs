using KSP.Localization;

namespace WOLF
{
    public static class RewardsManager
    {
        private static readonly string FUNDS_ADDED_MESSAGE = "#autoLOC_USI_WOLF_REWARDS_FUNDS_ADDED_MESSAGE";  // "You gained {0} Funds!";
        private static readonly string SCIENCE_ADDED_MESSAGE = "#autoLOC_USI_WOLF_REWARDS_SCIENCE_ADDED_MESSAGE";  // "You gained {0} Science!";
        private static readonly string REPUTATION_ADDED_MESSAGE = "#autoLOC_USI_WOLF_REWARDS_REPUTATION_ADDED_MESSAGE";  // "You gained {0} Reputation!";

        static RewardsManager()
        {
            if (Localizer.TryGetStringByTag("#autoLOC_USI_WOLF_REWARDS_FUNDS_ADDED_MESSAGE", out string fundsAddedMessage))
            {
                FUNDS_ADDED_MESSAGE = fundsAddedMessage;
            }
            if (Localizer.TryGetStringByTag("#autoLOC_USI_WOLF_REWARDS_REPUTATION_ADDED_MESSAGE", out string repAddedMessage))
            {
                REPUTATION_ADDED_MESSAGE = repAddedMessage;
            }
            if (Localizer.TryGetStringByTag("#autoLOC_USI_WOLF_REWARDS_SCIENCE_ADDED_MESSAGE", out string scienceAddedMessage))
            {
                SCIENCE_ADDED_MESSAGE = scienceAddedMessage;
            }
        }

        public static void AddTransportFunds(int payload)
        {
            if (Funding.Instance != null)
            {
                var funds = WOLF_GameParameters.TransportFundsRewardValue * payload;
                if (funds > 0)
                {
                    Funding.Instance.AddFunds(funds, TransactionReasons.ContractReward);
                    Messenger.DisplayMessage(string.Format(FUNDS_ADDED_MESSAGE, funds.ToString("F0")));
                }
            }
        }

        public static void AddDepotFunds(bool isHomeworld)
        {
            if (Funding.Instance != null)
            {
                var funds = isHomeworld
                    ? WOLF_GameParameters.DepotFundsRewardHomeworldValue
                    : WOLF_GameParameters.DepotFundsRewardValue;
                if (funds > 0)
                {
                    Funding.Instance.AddFunds(funds, TransactionReasons.ContractReward);
                    Messenger.DisplayMessage(string.Format(FUNDS_ADDED_MESSAGE, funds.ToString("F0")));
                }
            }
        }

        public static void AddReputation(int crewPoints, bool isHomeworld)
        {
            if (Reputation.Instance != null)
            {
                var configValue = isHomeworld
                    ? WOLF_GameParameters.CrewReputationRewardHomeworldValue
                    : WOLF_GameParameters.CrewReputationRewardValue;
                float rep = configValue * crewPoints;
                if (rep > 0)
                {
                    Reputation.Instance.AddReputation(rep, TransactionReasons.ContractReward);
                    Messenger.DisplayMessage(string.Format(REPUTATION_ADDED_MESSAGE, rep.ToString("F0")));
                }
            }
        }

        public static void AddScience(bool isHomeworld)
        {
            if (ResearchAndDevelopment.Instance != null)
            {
                var science = isHomeworld
                    ? WOLF_GameParameters.SurveyScienceRewardHomeworldValue
                    : WOLF_GameParameters.SurveyScienceRewardValue;
                if (science > 0)
                {
                    ResearchAndDevelopment.Instance.AddScience(science, TransactionReasons.ContractReward);
                    Messenger.DisplayMessage(string.Format(SCIENCE_ADDED_MESSAGE, science.ToString("F0")));
                }
            }
        }
    }
}
