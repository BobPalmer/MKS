using KSP.Localization;
using System.Linq;
using UnityEngine;

namespace WOLF
{
    public class WOLF_TerminalModule : WOLF_AbstractPartModule, ICorporeal
    {
        protected const string PAW_GROUP_NAME = "usi-wolf-terminal";

        protected WOLF_CrewTransferScenario _transferScenario;

        #region KSP fields
        [KSPField(isPersistant = true)]
        public string TerminalId;
        #endregion

        #region KSP actions and events
        [KSPEvent(
            guiName = "#LOC_USI_WOLF_PAW_TerminalModule_ShowWindowDisplayName",
            guiActive = false,
            guiActiveEditor = false,
            groupName = PAW_GROUP_NAME,
            groupDisplayName = "#LOC_USI_WOLF_PAW_TerminalModule_GroupDisplayName")]
        public void ShowWindowEvent()
        {
            if (_transferScenario != null)
            {
                var body = vessel.mainBody.name;
                var biome = GetVesselBiome(vessel);
                _transferScenario.ShowWindow(body, biome);
            }
        }
        #endregion

        public IDepot Depot { get; protected set; }
        public bool IsConnectedToDepot { get; protected set; }

        protected override void ConnectToDepot()
        {
            var body = vessel.mainBody.name;
            var biome = GetVesselBiome(vessel);
            if (biome == string.Empty)
            {
                Messenger.DisplayMessage(Messenger.INVALID_SITUATION_MESSAGE);
                return;
            }
            if (biome.StartsWith("Orbit") && biome != "Orbit")
            {
                Messenger.DisplayMessage(Messenger.INVALID_ORBIT_SITUATION_MESSAGE);
                return;
            }
            if (!_registry.HasEstablishedDepot(body, biome))
            {
                Messenger.DisplayMessage(Messenger.MISSING_DEPOT_MESSAGE);
                return;
            }
            var attachedDepotModules
                = vessel.FindPartModulesImplementing<WOLF_DepotModule>();
            if (attachedDepotModules.Any())
            {
                Messenger.DisplayMessage(Messenger.INVALID_DEPOT_PART_ATTACHMENT_MESSAGE);
                return;
            }
            var nonCorporealModules = vessel
                .FindPartModulesImplementing<WOLF_AbstractPartModule>()
                .Where(p => !(p is ICorporeal));
            if (nonCorporealModules.Any())
            {
                Messenger.DisplayMessage(Messenger.INVALID_HOPPER_PART_ATTACHMENT_MESSAGE);
                return;
            }

            var depot = _registry.GetDepot(body, biome);
            TerminalId = _registry.CreateTerminal(depot);
            IsConnectedToDepot = true;
            Actions[nameof(ConnectToDepotAction)].active = false;
            Events[nameof(ConnectToDepotEvent)].guiActive = false;
            Events[nameof(ShowWindowEvent)].guiActive = true;

            // Hook into vessel destroyed events to clean up scenario registry
            if (vessel != null)
            {
                vessel.OnJustAboutToBeDestroyed += OnVesselDestroyed;
                GameEvents.OnVesselRecoveryRequested.Add(OnVesselRecovered);
            }

            Messenger.DisplayMessage(string.Format(Messenger.SUCCESSFUL_DEPLOYMENT_MESSAGE, body));
        }

        protected void GetLocalizedDisplayNames()
        {
            if (Localizer.TryGetStringByTag(
                "#autoLOC_USI_WOLF_CONNECT_TO_DEPOT_GUI_NAME",
                out var connectToDepotGuiName))
            {
                Actions[nameof(ConnectToDepotAction)].guiName = connectToDepotGuiName;
                Events[nameof(ConnectToDepotEvent)].guiName = connectToDepotGuiName;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_PAW_TerminalModule_GroupDisplayName",
                out var pawGroupDisplayName))
            {
                Events[nameof(ConnectToDepotEvent)].group.displayName = pawGroupDisplayName;
                Events[nameof(ShowWindowEvent)].group.displayName = pawGroupDisplayName;
            }
            if (Localizer.TryGetStringByTag(
                "LOC_USI_WOLF_PAW_TerminalModule_ShowWindowDisplayName",
                out var showWindowDisplayName))
            {
                Events[nameof(ShowWindowEvent)].guiName = showWindowDisplayName;
            }
        }

        public override void OnAwake()
        {
            base.OnAwake();

            GetLocalizedDisplayNames();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            // Wire up the scenario module
            _transferScenario = FindObjectOfType<WOLF_CrewTransferScenario>();

            // Determine if terminal is connected to a depot already
            if (!string.IsNullOrEmpty(TerminalId))
            {
                var body = vessel.mainBody.name;
                var biome = GetVesselBiome(vessel);
                var depot = _registry.GetDepot(body, biome);
                IsConnectedToDepot = _registry.HasTerminal(TerminalId, depot);
                if (!IsConnectedToDepot)
                {
                    TerminalId = string.Empty;
                }
                else
                {
                    Depot = depot;
                    Actions[nameof(ConnectToDepotAction)].active = false;
                    Events[nameof(ConnectToDepotEvent)].guiActive = false;
                    Events[nameof(ShowWindowEvent)].guiActive = true;
                }
            }
        }

        public void OnVesselDestroyed()
        {
            Debug.Log("[WOLF] Vessel with attached terminal was destroyed.");
            if (IsConnectedToDepot)
            {
                _registry.RemoveTerminal(TerminalId);
            }
            vessel.OnJustAboutToBeDestroyed -= OnVesselDestroyed;
            GameEvents.OnVesselRecoveryRequested.Remove(OnVesselRecovered);
        }

        public void OnVesselRecovered(Vessel vessel)
        {
            if (vessel == this.vessel)
            {
                Debug.Log("[WOLF] Vessel with attached terminal was recovered.");
                OnVesselDestroyed();
            }
        }
    }
}
