namespace WOLF
{
    public class WOLF_GameParameters : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomIntParameterUI(
            "#autoLOC_WOLF_RESOURCE_ABUNDANCE_MULTIPLIER_OPTION_TITLE",
            autoPersistance = true, minValue = 1, maxValue = 10, stepSize = 1,
            toolTip = "#autoLOC_WOLF_RESOURCE_ABUNDANCE_MULTIPLIER_OPTION_TOOLTIP")]
        public int ResourceAbundanceMultiplier = 1;

        [GameParameters.CustomIntParameterUI(
            "#autoLOC_WOLF_DEPOT_FUNDS_REWARD_HOMEWORLD_OPTION_TITLE",
            autoPersistance = true, minValue = 0, maxValue = 50000, stepSize = 1000,
            toolTip = "#autoLOC_WOLF_DEPOT_FUNDS_REWARD_HOMEWORLD_OPTION_TOOLTIP")]
        public int DepotFundsRewardHomeworld = 10000;

        [GameParameters.CustomIntParameterUI(
            "#autoLOC_WOLF_DEPOT_FUNDS_REWARD_OPTION_TITLE",
            autoPersistance = true, minValue = 0, maxValue = 100000, stepSize = 1000,
            toolTip = "#autoLOC_WOLF_DEPOT_FUNDS_REWARD_OPTION_TOOLTIP")]
        public int DepotFundsReward = 50000;

        [GameParameters.CustomIntParameterUI(
            "#autoLOC_WOLF_TRANSPORT_FUNDS_REWARD_OPTION_TITLE",
            autoPersistance = true, minValue = 0, maxValue = 50000, stepSize = 1000,
            toolTip = "#autoLOC_WOLF_TRANSPORT_FUNDS_REWARD_OPTION_TOOLTIP")]
        public int TransportFundsReward = 10000;

        [GameParameters.CustomIntParameterUI(
            "#autoLOC_WOLF_SURVEY_SCIENCE_REWARD_HOMEWORLD_OPTION_TITLE",
            autoPersistance = true, minValue = 0, maxValue = 100, stepSize = 10,
            toolTip = "#autoLOC_WOLF_SURVEY_SCIENCE_REWARD_HOMEWORLD_OPTION_TOOLTIP")]
        public int SurveyScienceRewardHomeworld = 50;

        [GameParameters.CustomIntParameterUI(
            "#autoLOC_WOLF_SCIENCE_SURVEY_REWARD_OPTION_TITLE",
            autoPersistance = true, minValue = 0, maxValue = 1000, stepSize = 50,
            toolTip = "#autoLOC_WOLF_SCIENCE_SURVEY_REWARD_OPTION_TOOLTIP")]
        public int SurveyScienceReward = 250;

        [GameParameters.CustomIntParameterUI(
            "#autoLOC_WOLF_CREW_REPUTATION_REWARD_HOMEWORLD_OPTION_TITLE",
            autoPersistance = true, minValue = 0, maxValue = 20, stepSize = 1,
            toolTip = "#autoLOC_WOLF_CREW_REPUTATION_REWARD_HOMEWORLD_OPTION_TOOLTIP")]
        public int CrewReputationRewardHomeworld = 5;

        [GameParameters.CustomIntParameterUI(
            "#autoLOC_WOLF_CREW_REPUTATION_REWARD_OPTION_TITLE",
            autoPersistance = true, minValue = 0, maxValue = 50, stepSize = 5,
            toolTip = "#autoLOC_WOLF_CREW_REPUTATION_REWARD_OPTION_TOOLTIP")]
        public int CrewReputationReward = 10;

        public static int ResourceAbundanceMultiplierValue
        {
            get
            {
                var options = HighLogic.CurrentGame.Parameters.CustomParams<WOLF_GameParameters>();

                return options.ResourceAbundanceMultiplier;
            }
        }

        public static double DepotFundsRewardHomeworldValue
        {
            get
            {
                var options = HighLogic.CurrentGame.Parameters.CustomParams<WOLF_GameParameters>();

                return options.DepotFundsRewardHomeworld;
            }
        }

        public static double DepotFundsRewardValue
        {
            get
            {
                var options = HighLogic.CurrentGame.Parameters.CustomParams<WOLF_GameParameters>();

                return options.DepotFundsReward;
            }
        }

        public static int TransportFundsRewardValue
        {
            get
            {
                var options = HighLogic.CurrentGame.Parameters.CustomParams<WOLF_GameParameters>();

                return options.TransportFundsReward;
            }
        }

        public static float SurveyScienceRewardHomeworldValue
        {
            get
            {
                var options = HighLogic.CurrentGame.Parameters.CustomParams<WOLF_GameParameters>();

                return options.SurveyScienceRewardHomeworld;
            }
        }

        public static float SurveyScienceRewardValue
        {
            get
            {
                var options = HighLogic.CurrentGame.Parameters.CustomParams<WOLF_GameParameters>();

                return options.SurveyScienceReward;
            }
        }

        public static int CrewReputationRewardHomeworldValue
        {
            get
            {
                var options = HighLogic.CurrentGame.Parameters.CustomParams<WOLF_GameParameters>();

                return options.CrewReputationRewardHomeworld;
            }
        }

        public static int CrewReputationRewardValue
        {
            get
            {
                var options = HighLogic.CurrentGame.Parameters.CustomParams<WOLF_GameParameters>();

                return options.CrewReputationReward;
            }
        }

        public override string Section => "WOLF";

        public override string DisplaySection => "WOLF";

        public override string Title => string.Empty;

        public override int SectionOrder => 1;

        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;

        public override bool HasPresets => false;
    }
}
