using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using USITools;

namespace WOLF
{
    public class WOLF_CrewTransporterModule : WOLF_TransporterModule
    {
        protected const int BERTH_COST_MULTIPLIER = 2;
        protected const float TERMINAL_RANGE = 500f;

        protected ICrewRouteRegistry _crewRouteRegistry;
        protected string _insufficientColonySuppliesMessage
            = "#LOC_USI_WOLF_CrewTransporterModule_InsufficientColonySuppliesMessage";
        protected string _insufficientHabitationMessage
            = "#LOC_USI_WOLF_CrewTransporterModule_InsufficientHabitationMessage";
        protected string _insufficientLifeSupportMessage
            = "#LOC_USI_WOLF_CrewTransporterModule_InsufficientLifeSupportMessage";
        protected string _noNearbyTerminalsMessage
            = "#LOC_USI_WOLF_Terminal_NoNearbyTerminalsMessage";

        #region KSP fields
        [KSPField(isPersistant = true)]
        public double RouteStart;
        #endregion

        protected override void CacheCargoModules()
        {
            var modules = vessel?.FindPartModulesImplementing<WOLF_CrewCargoModule>();
            if (modules != null)
            {
                _cargoModules = new List<ICargo>();
                foreach (var module in modules)
                {
                    _cargoModules.Add(module);
                }
            }
            else
            {
                _cargoModules = null;
            }
        }

        protected override IRoutePayload CalculateRoutePayload(bool verifyRoute = true)
        {
            if (_cargoModules == null || _cargoModules.Count < 1)
            {
                return CrewRoutePayload.Zero;
            }

            var cargoModules = _cargoModules
                .Where(m => !verifyRoute || m.VerifyRoute(RouteId));

            var berths = cargoModules
                .Select(m => (CrewPayload)m.GetPayload());
            var economyBerths = berths.Sum(b => b.EconomyBerths);
            var luxuryBerths = berths.Sum(b => b.LuxuryBerths);

            return new CrewRoutePayload(economyBerths, luxuryBerths);
        }

        protected override bool CanConnectToOrigin()
        {
            if (!base.CanConnectToOrigin())
            {
                return false;
            }
            if (!IsNearTerminal())
            {
                DisplayMessage(string.Format(
                    _noNearbyTerminalsMessage,
                    TERMINAL_RANGE));
                return false;
            }
            var berths = (CrewRoutePayload)CalculateRoutePayload(false);
            if (!berths.HasMinimumPayload(1))
            {
                DisplayMessage(INSUFFICIENT_PAYLOAD_MESSAGE);
                return false;
            }

            // Make sure origin has enough hab and life support
            var originBody = vessel.mainBody.name;
            var originBiome = GetVesselBiome();
            var originDepot = _registry.GetDepot(originBody, originBiome);
            return CheckBerthCost(originDepot, berths);
        }

        protected bool CheckBerthCost(IDepot depot, CrewRoutePayload routePayload)
        {
            var depotResources = depot.GetResources();
            var economyBerthCost = routePayload.EconomyBerths * BERTH_COST_MULTIPLIER;
            var luxuryBerthCost = routePayload.LuxuryBerths * BERTH_COST_MULTIPLIER;
            var totalBerthCost = economyBerthCost + luxuryBerthCost;
            var originHab = depotResources
                .FirstOrDefault(r => r.ResourceName == "Habitation");
            if (originHab == null)
            {
                DisplayMessage(string.Format(
                    _insufficientHabitationMessage,
                    totalBerthCost));
                return false;
            }
            if (originHab.Available < totalBerthCost)
            {
                DisplayMessage(string.Format(
                    _insufficientHabitationMessage,
                    totalBerthCost - originHab.Available));
                return false;
            }
            var originLifeSupport = depotResources
                .FirstOrDefault(r => r.ResourceName == "LifeSupport");
            if (originLifeSupport == null)
            {
                DisplayMessage(string.Format(
                    _insufficientLifeSupportMessage,
                    totalBerthCost));
                return false;
            }
            if (originLifeSupport.Available < totalBerthCost)
            {
                DisplayMessage(string.Format(
                    _insufficientLifeSupportMessage,
                    totalBerthCost - originLifeSupport.Available));
                return false;
            }
            if (luxuryBerthCost > 0)
            {
                var originColonySupplies = depotResources
                    .FirstOrDefault(r => r.ResourceName == "ColonySupplies");
                if (originColonySupplies == null)
                {
                    DisplayMessage(string.Format(
                        _insufficientColonySuppliesMessage,
                        luxuryBerthCost));
                    return false;
                }
                if (originColonySupplies.Available < luxuryBerthCost)
                {
                    DisplayMessage(string.Format(
                        _insufficientColonySuppliesMessage,
                        luxuryBerthCost - originColonySupplies.Available));
                    return false;
                }
            }
            return true;
        }

        // We'll piggyback on the base class ConnectToDepotEvent to
        //   handle making the connection on the destination side
        protected override void ConnectToDepot()
        {
            // Check for issues that would prevent deployment
            var deployCheckResult = CanConnectToDepot();
            if (!string.IsNullOrEmpty(deployCheckResult))
            {
                DisplayMessage(deployCheckResult);
                return;
            }

            var destinationBody = vessel.mainBody.name;
            var destinationBiome = GetVesselBiome();
            if (destinationBody == OriginBody && destinationBiome == OriginBiome)
            {
                DisplayMessage(INVALID_CONNECTION_MESSAGE);
                return;
            }

            var routePayload = CalculateRoutePayload();
            if (!routePayload.HasMinimumPayload(MINIMUM_PAYLOAD))
            {
                DisplayMessage(INSUFFICIENT_PAYLOAD_MESSAGE);
                return;
            }

            try
            {
                if (TryNegotiateRoute(destinationBody, destinationBiome, routePayload))
                {
                    // Add rewards
                    var homeworld = FlightGlobals.GetHomeBodyName();
                    if (destinationBody == homeworld && OriginBody != destinationBody)
                    {
                        RewardsManager.AddTransportFunds(routePayload.GetRewards());
                    }

                    ResetRoute();
                }
            }
            catch (Exception ex)
            {
                DisplayMessage(ex.Message);
            }
        }

        protected override void ConnectToOrigin()
        {
            base.ConnectToOrigin();

            RouteStart = Planetarium.GetUniversalTime();
        }

        protected override void GetLocalizedTextValues()
        {
            base.GetLocalizedTextValues();

            Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_CrewTransporterModule_InsufficientHabitationMessage",
                out _insufficientHabitationMessage);
            Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_CrewTransporterModule_InsufficientLifeSupportMessage",
                out _insufficientLifeSupportMessage);
            Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_CrewTransporter_NoNearbyTerminalsMessage",
                out _noNearbyTerminalsMessage);

            var pawGroupName = "usi-wolf-crew-transporter";
            if (!Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_PAW_CrewTransporterModule_GroupDisplayName",
                out string pawGroupDisplayName))
            {
                pawGroupDisplayName = "#LOC_USI_WOLF_PAW_CrewTransporterModule_GroupDisplayName";
            }

            Events[nameof(CancelRouteEvent)].group.name = pawGroupName;
            Events[nameof(CancelRouteEvent)].group.displayName = pawGroupDisplayName;
            Events[nameof(ConnectToOriginEvent)].group.name = pawGroupName;
            Events[nameof(ConnectToOriginEvent)].group.displayName = pawGroupDisplayName;
            Events[nameof(ConnectToDepotEvent)].group.name = pawGroupName;
            Events[nameof(ConnectToDepotEvent)].group.displayName = pawGroupDisplayName;
            Fields[nameof(OriginDepotDisplay)].group.name = pawGroupName;
            Fields[nameof(OriginDepotDisplay)].group.displayName = pawGroupDisplayName;
            Fields[nameof(RouteCost)].group.name = pawGroupName;
            Fields[nameof(RouteCost)].group.displayName = pawGroupDisplayName;
            Fields[nameof(RoutePayload)].group.name = pawGroupName;
            Fields[nameof(RoutePayload)].group.displayName = pawGroupDisplayName;
            Fields[nameof(RoutePayloadCost)].group.name = pawGroupName;
            Fields[nameof(RoutePayloadCost)].group.displayName = pawGroupDisplayName;
        }

        protected bool IsNearTerminal()
        {
            var partModules = LogisticsTools.GetNearbyPartModules<WOLF_TerminalModule>(
                TERMINAL_RANGE,
                vessel,
                false,
                false);
            var body = vessel.mainBody.name;
            var biome = GetVesselBiome();

            return partModules != null && partModules.Any(m =>
                m.IsConnectedToDepot &&
                m.Depot != null &&
                m.Depot.Body == body &&
                m.Depot.Biome == biome);
        }

        protected override bool TryNegotiateRoute(
            string destinationBody,
            string destinationBiome,
            IRoutePayload routePayload)
        {
            if (!IsNearTerminal())
            {
                DisplayMessage(string.Format(
                    _noNearbyTerminalsMessage,
                    TERMINAL_RANGE));
                return false;
            }

            var originDepot = _registry.GetDepot(OriginBody, OriginBiome);
            var routeCost = CalculateRouteCost();
            if (routeCost > 0)
            {
                // Make sure origin depot has enough TransportCredits to support the route
                var originTransportCredits = originDepot.GetResources()
                    .Where(r => r.ResourceName == "TransportCredits")
                    .FirstOrDefault();
                if (originTransportCredits == null)
                {
                    DisplayMessage(string.Format(
                        INSUFFICIENT_TRANSPORT_CREDITS_MESSAGE,
                        routeCost));
                    return false;
                }
                if (originTransportCredits.Available < routeCost)
                {
                    DisplayMessage(string.Format(
                        INSUFFICIENT_TRANSPORT_CREDITS_MESSAGE,
                        routeCost - originTransportCredits.Available));
                    return false;
                }
            }

            // Make sure origin depot has enough hab & life support
            var crewRoutePayload = (CrewRoutePayload)routePayload;
            if (!CheckBerthCost(originDepot, crewRoutePayload))
            {
                return false;
            }

            // Calculate duration
            var now = Planetarium.GetUniversalTime();
            var duration = now - RouteStart;

            // Create route and consume route costs
            var economyBerthCost = crewRoutePayload.EconomyBerths * BERTH_COST_MULTIPLIER;
            var luxuryBerthCost = crewRoutePayload.LuxuryBerths * BERTH_COST_MULTIPLIER;
            var totalBerthCost = economyBerthCost + luxuryBerthCost;
            _registry.CreateCrewRoute(
                OriginBody,
                OriginBiome,
                destinationBody,
                destinationBiome,
                crewRoutePayload.EconomyBerths,
                crewRoutePayload.LuxuryBerths,
                duration);
            if (routeCost > 0)
            {
                originDepot.NegotiateConsumer(new Dictionary<string, int>
                {
                    { "TransportCredits", routeCost }
                });
            }
            originDepot.NegotiateConsumer(new Dictionary<string, int>
            {
                { "Habitation", totalBerthCost },
                { "LifeSupport", totalBerthCost },
            });
            if (luxuryBerthCost > 0)
            {
                originDepot.NegotiateConsumer(new Dictionary<string, int>
                {
                    { "ColonySupplies", luxuryBerthCost }
                });
            }

            if (OriginBody == destinationBody)
            {
                DisplayMessage(string.Format(
                    Messenger.SUCCESSFUL_DEPLOYMENT_MESSAGE,
                    OriginBody));
            }
            else
            {
                DisplayMessage(string.Format(
                    Messenger.SUCCESSFUL_DEPLOYMENT_MESSAGE,
                    $"{OriginBody} and {destinationBody}"));
            }

            return true;
        }
    }
}
