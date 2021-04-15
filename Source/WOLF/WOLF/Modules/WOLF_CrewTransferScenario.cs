using KSP.Localization;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using UnityEngine;
using USITools;
using WOLFUI;

namespace WOLF
{
    [KSPScenario(
        ScenarioCreationOptions.AddToAllGames,
        GameScenes.FLIGHT,
        GameScenes.SPACECENTER,
        GameScenes.TRACKSTATION)]
    public class WOLF_CrewTransferScenario : ScenarioModule, ICrewTransferController
    {
        private const float TERMINAL_RANGE = 500f;

        private readonly ArrivalWarnings _arrivalWarnings = new ArrivalWarnings();
        private readonly Dictionary<string, WarningMetadata> _activeWarnings
            = new Dictionary<string, WarningMetadata>();
        private double _nextLazyUpdate;
        private readonly Dictionary<string, PassengerMetadata> _passengers
            = new Dictionary<string, PassengerMetadata>();
        private ICrewRoute _selectedFlight;
        private ArrivalMetadata _selectedTerminal;
        private ApplicationLauncherButton _toolbarButton;
        private TerminalWindow _window;
        private IRegistryCollection _wolf;

        public ActionPanelLabels ArrivalPanelLabels { get; private set; }
            = new ActionPanelLabels();
        public string ArrivalsHeaderLabel { get; private set; }
        public Canvas Canvas => MainCanvasUtil.MainCanvas;
        public string DeparturesHeaderLabel { get; private set; }
        public ActionPanelLabels DeparturePanelLabels { get; private set; }
            = new ActionPanelLabels();
        public ActionPanelLabels EnroutePanelLabels { get; private set; }
            = new ActionPanelLabels();
        public string FlightsHeaderLabel { get; private set; }
        public FlightSelectorLabels FlightSelectorLabels { get; private set; }
            = new FlightSelectorLabels();
        public SelectedFlightPanelLabels SelectedFlightPanelLabels { get; private set; }
            = new SelectedFlightPanelLabels();
        public string TitleBarLabel { get; private set; }

        public void ArrivalTerminalSelected(ArrivalMetadata terminal)
        {
            _selectedTerminal = terminal;
            if (_selectedTerminal == null)
            {
                ShowWarning(_arrivalWarnings.NoTerminalSelectedMessage, true);
            }
            else
            {
                CheckBerths();
                ClearWarning(_arrivalWarnings.NoTerminalSelectedMessage);
            }
        }

        private void CheckBerths()
        {
            _activeWarnings.Clear();
            var passengers = _passengers.Values;
            if (_selectedFlight != null)
            {
                if (passengers == null || passengers.Count < 1)
                {
                    ShowWarning(SelectedFlightPanelLabels.NoPassengersSelectedMessage, true);
                }
                else
                {
                    var tourists = passengers.Count(p => p.IsTourist);
                    var crew = passengers.Count - tourists;
                    var totalPassengers = tourists + crew;
                    var touristBerths = _selectedFlight.LuxuryBerths;
                    var economyBerths = _selectedFlight.EconomyBerths;
                    var totalBerths = touristBerths + economyBerths;
                    var extraCrew = Math.Max(0, crew - economyBerths);
                    if (_selectedFlight.FlightStatus == FlightStatus.Boarding)
                    {
                        if (tourists > touristBerths || extraCrew + tourists > touristBerths)
                        {
                            ShowWarning(SelectedFlightPanelLabels.InsufficientTouristBerths, true);
                        }
                        else if (totalPassengers > totalBerths)
                        {
                            ShowWarning(SelectedFlightPanelLabels.InsufficientBerths, true);
                        }
                        else if (totalPassengers < totalBerths)
                        {
                            ShowWarning(SelectedFlightPanelLabels.FlightNotFullMessage, false);
                        }
                    }
                    else if (_selectedTerminal != null &&
                        _selectedTerminal.OccupiedSeats > _selectedTerminal.TotalSeats)
                    {
                        ShowWarning(_arrivalWarnings.TooManyPassengersSelectedMessage, true);
                    }
                }
            }
        }

        public void ClearWarning(string message)
        {
            if (_activeWarnings.ContainsKey(message))
            {
                _activeWarnings.Remove(message);
            }
            _window.ShowWarnings(_activeWarnings.Values.ToList());
        }

        public void CloseWindow()
        {
            if (_window != null)
            {
                _window.CloseWindow();
            }
        }

        public void Disembark()
        {
            if (_selectedTerminal != null &&
                _passengers.Count > 0 &&
                !_activeWarnings.Any(w => w.Value.PreventsAction))
            {
                var terminalVessel = FlightGlobals.Vessels
                    .FirstOrDefault(v => v.id == _selectedTerminal.VesselId);

                if (terminalVessel != null)
                {
                    var terminal = terminalVessel
                        .FindPartModulesImplementing<WOLF_TerminalModule>()
                        .FirstOrDefault(t => t.TerminalId == _selectedTerminal.TerminalId);

                    if (terminal != null)
                    {
                        foreach (var passenger in _passengers)
                        {
                            var kerbal = HighLogic.CurrentGame.CrewRoster.Kerbals()
                                .FirstOrDefault(k => k.name == passenger.Value.Name);
                            if (kerbal != null)
                            {
                                kerbal.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                                terminal.part.AddCrewmember(kerbal);
                            }
                            else
                            {
                                Debug.LogError($"[WOLF] CrewTransferScenario.Disembark: Could not find {passenger.Value.Name} in current game crew roster.");
                            }
                            var wolfPassenger = _selectedFlight.Passengers
                                .FirstOrDefault(p => p.Name == passenger.Value.Name);
                            _selectedFlight.Disembark(wolfPassenger);
                        }
                        _passengers.Clear();
                        terminalVessel.CrewListSetDirty();
                        terminalVessel.MakeActive();
                        KSP.UI.Screens.Flight.KerbalPortraitGallery.Instance.StartRefresh(terminalVessel);

                        // Check if flight has finished disembarking all passengers
                        var flight = _wolf.GetCrewRoute(_selectedFlight.UniqueId);
                        if (flight.FlightStatus != FlightStatus.Arrived)
                        {
                            _selectedFlight = null;
                        }
                    }
                }
            }
        }

        public void FlightSelected(FlightMetadata flightMetadata)
        {
            _activeWarnings.Clear();
            _passengers.Clear();
            if (flightMetadata == null)
            {
                _selectedFlight = null;
            }
            else if (_selectedFlight == null ||
                _selectedFlight.UniqueId != flightMetadata.UniqueId)
            {
                _selectedFlight = _wolf.GetCrewRoute(flightMetadata.UniqueId);
                if (flightMetadata.Status == FlightStatus.Boarding ||
                    flightMetadata.Status == FlightStatus.Arrived)
                {
                    ShowWarning(SelectedFlightPanelLabels.NoPassengersSelectedMessage, true);
                }
            }
        }

        private string GetDuration(ICrewRoute flight)
        {
            if (flight.FlightStatus == FlightStatus.Enroute)
            {
                var remaining = flight.ArrivalTime - Planetarium.GetUniversalTime();
                return TimeFormatters.ToTimeSpanString(remaining);
            }
            else
            {
                return TimeFormatters.ToTimeSpanString(flight.Duration);
            }
        }

        private string GetEconomySeats(ICrewRoute flight)
        {
            if (flight.FlightStatus == FlightStatus.Boarding)
            {
                var passengers = Math.Min(
                    _passengers.Values.Count(p => !p.IsTourist),
                    flight.EconomyBerths);
                return $"{passengers} / {flight.EconomyBerths}";
            }
            else
            {
                var passengers = flight.Passengers.Count(p => !p.IsTourist);
                return $"{passengers} / {flight.EconomyBerths}";
            }
        }

        #region Localization
        private void GetLocalizedTextValues()
        {
            // Main window labels
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_CrewTransferWindow_ArrivalsHeaderLabel",
                out var arrivalsHeaderLabel))
            {
                ArrivalsHeaderLabel = arrivalsHeaderLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_CrewTransferWindow_DeparturesHeaderLabel",
                out var departuresHeaderLabel))
            {
                DeparturesHeaderLabel = departuresHeaderLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_CrewTransferWindow_FlightsHeaderLabel",
                out var flightsHeaderLabel))
            {
                FlightsHeaderLabel = flightsHeaderLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_CrewTransferWindow_TitleBarLabel",
                out var titleBarLabel))
            {
                TitleBarLabel = titleBarLabel;
            }
            // Arrival warning messages
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_ArrivalWarnings_NoNearbyTerminalsMessage",
                out var noNearbyTerminalsMessage))
            {
                _arrivalWarnings.NoNearbyTerminalsMessage = noNearbyTerminalsMessage;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_ArrivalWarnings_NoTerminalSelectedMessage",
                out var noTerminalSelectedMessage))
            {
                _arrivalWarnings.NoTerminalSelectedMessage = noTerminalSelectedMessage;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_ArrivalWarnings_TooManyPassengersSelectedMessage",
                out var tooManyPassengersMessage))
            {
                _arrivalWarnings.TooManyPassengersSelectedMessage = tooManyPassengersMessage;
            }
            // Flight selector labels
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_FlightSelector_ArrivalHeaderLabel",
                out var arrivalHeaderLabel))
            {
                FlightSelectorLabels.ArrivalHeaderLabel = arrivalHeaderLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_FlightSelector_DepartureHeaderLabel",
                out var departureHeaderLabel))
            {
                FlightSelectorLabels.DepartureHeaderLabel = departureHeaderLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_FlightSelector_FlightNumberPrefx",
                out var flightNumberPrefix))
            {
                FlightSelectorLabels.FlightNumberPrefx = flightNumberPrefix;
                SelectedFlightPanelLabels.FlightNumberPrefix = flightNumberPrefix;
            }
            // Selected flight panel labels
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_ArrivalHeaderLabel",
                out var arrivalHeaderLabel2))
            {
                SelectedFlightPanelLabels.ArrivalHeaderLabel = arrivalHeaderLabel2;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_ArrivedStatus",
                out var arrivedStatusText))
            {
                SelectedFlightPanelLabels.ArrivedText = arrivedStatusText;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_BoardingStatus",
                out var boardStatusText))
            {
                SelectedFlightPanelLabels.BoardingText = boardStatusText;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_DepartureHeaderLabel",
                out var departureHeaderLabel2))
            {
                SelectedFlightPanelLabels.DepartureHeaderLabel = departureHeaderLabel2;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_EconomySeatsHeaderLabel",
                out var economySeatsLabel))
            {
                SelectedFlightPanelLabels.EconomySeatsHeaderLabel = economySeatsLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_EnrouteStatus",
                out var enrouteStatusText))
            {
                SelectedFlightPanelLabels.EnrouteText = enrouteStatusText;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_FlightNotFullMessage",
                out var flightNotFullMessage))
            {
                SelectedFlightPanelLabels.FlightNotFullMessage = flightNotFullMessage;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_InsufficientBerthsMessage",
                out var insufficentBerthsMessage))
            {
                SelectedFlightPanelLabels.InsufficientBerths = insufficentBerthsMessage;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_InsufficientTouristBerthsMessage",
                out var insufficientTouristBerthsMessage))
            {
                SelectedFlightPanelLabels.InsufficientTouristBerths = insufficientTouristBerthsMessage;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_LuxurySeatsHeaderLabel",
                out var luxurySeatsLabel))
            {
                SelectedFlightPanelLabels.LuxurySeatsHeaderLabel = luxurySeatsLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_NoKerbalsToBoardMessage",
                out var noKerbalsMessage))
            {
                SelectedFlightPanelLabels.NoKerbalsToBoardMessage = noKerbalsMessage;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_SelectedFlightPanel_NoPassengersSelectedMessage",
                out var noPassengersSelectedMessage))
            {
                SelectedFlightPanelLabels.NoPassengersSelectedMessage = noPassengersSelectedMessage;
            }
            // Action panel labels
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_ActionPanels_WarningLabel",
                out var warningLabel))
            {
                ArrivalPanelLabels.WarningLabel = warningLabel;
                DeparturePanelLabels.WarningLabel = warningLabel;
                EnroutePanelLabels.WarningLabel = warningLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_ArrivalPanel_ActionButtonLabel",
                out var arrivalActionButtonLabel))
            {
                ArrivalPanelLabels.ActionButtonLabel = arrivalActionButtonLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_ArrivalPanel_HeaderLabel",
                out var arrivalPanelHeader))
            {
                ArrivalPanelLabels.HeaderLabel = arrivalPanelHeader;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_ArrivalPanel_Instructions",
                out var arrivalPanelInstructions))
            {
                ArrivalPanelLabels.InstructionsLabel = arrivalPanelInstructions;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_DeparturePanel_ActionButtonLabel",
                out var departureActionButtonLabel))
            {
                DeparturePanelLabels.ActionButtonLabel = departureActionButtonLabel;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_DeparturePanel_HeaderLabel",
                out var departurePanelHeader))
            {
                DeparturePanelLabels.HeaderLabel = departurePanelHeader;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_DeparturePanel_Instructions",
                out var departurePanelInstructions))
            {
                DeparturePanelLabels.InstructionsLabel = departurePanelInstructions;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_EnroutePanel_HeaderLabel",
                out var enroutePanelHeader))
            {
                EnroutePanelLabels.HeaderLabel = enroutePanelHeader;
            }
            if (Localizer.TryGetStringByTag(
                "#LOC_USI_WOLF_EnroutePanel_Instructions",
                out var enroutePanelInstructions))
            {
                EnroutePanelLabels.InstructionsLabel = enroutePanelInstructions;
            }
        }
        #endregion

        private string GetLuxurySeats(ICrewRoute flight)
        {
            if (flight.FlightStatus == FlightStatus.Boarding)
            {
                var passengers = _passengers.Values;
                var tourists = passengers.Count(p => p.IsTourist);
                var extraCrew = Math.Min(
                    flight.EconomyBerths - passengers.Count(p => !p.IsTourist),
                    0);
                return $"{tourists + extraCrew} / {flight.LuxuryBerths}";
            }
            else
            {
                var passengers = flight.Passengers.Count(p => p.IsTourist);
                return $"{passengers} / {flight.LuxuryBerths}";
            }
        }

        private List<ProtoCrewMember> GetNearbyKerbals()
        {
            var activeVessel = FlightGlobals.ActiveVessel;
            if (activeVessel == null)
            {
                return null;
            }
            // Make sure we have an eligible terminal nearby
            var landedSituations = Vessel.Situations.LANDED |
                Vessel.Situations.PRELAUNCH |
                Vessel.Situations.SPLASHED;
            var landedOnly = (activeVessel.situation & landedSituations) == landedSituations;
            var terminals = LogisticsTools.GetNearbyPartModules<WOLF_TerminalModule>(
                TERMINAL_RANGE,
                activeVessel,
                true,
                landedOnly);
            if (terminals == null || terminals.Count < 1)
            {
                return null;
            }
            var terminalIds = terminals
                .Where(t => !string.IsNullOrEmpty(t.TerminalId))
                .Select(t => t.TerminalId);
            var wolfTerminals = _wolf.GetTerminals()
                .Where(t => terminalIds.Contains(t.Id) &&
                    t.Body == _selectedFlight.OriginBody &&
                    t.Biome == _selectedFlight.OriginBiome);
            if (wolfTerminals == null || wolfTerminals.Count() < 1)
            {
                return null;
            }
            // Get all nearby vessels without command seats
            var vessels = LogisticsTools
                .GetNearbyVessels(TERMINAL_RANGE, true, activeVessel, landedOnly)
                .Where(v => !v.parts.Any(p => p.FindModulesImplementing<KerbalSeat>().Any()));
            var kerbals = new List<ProtoCrewMember>();
            foreach (var vessel in vessels)
            {
                var crew = vessel
                    .GetVesselCrew()
                    .Where(c => c.type == ProtoCrewMember.KerbalType.Crew ||
                        c.type == ProtoCrewMember.KerbalType.Tourist);
                kerbals.AddRange(crew);
            }
            return kerbals;
        }

        private List<ArrivalMetadata> GetNearbyTerminals()
        {
            var activeVessel = FlightGlobals.ActiveVessel;
            if (activeVessel == null)
            {
                return null;
            }
            // Make sure we have an eligible terminal nearby
            var landedSituations = Vessel.Situations.LANDED |
                Vessel.Situations.PRELAUNCH |
                Vessel.Situations.SPLASHED;
            var landedOnly = (activeVessel.situation & landedSituations) == landedSituations;
            var terminalModules = LogisticsTools.GetNearbyPartModules<WOLF_TerminalModule>(
                TERMINAL_RANGE,
                activeVessel,
                true,
                landedOnly);
            if (terminalModules == null || terminalModules.Count < 1)
            {
                return null;
            }
            var terminalIds = terminalModules
                .Where(t => !string.IsNullOrEmpty(t.TerminalId))
                .Select(t => t.TerminalId);
            var wolfTerminals = _wolf.GetTerminals()
                .Where(t => terminalIds.Contains(t.Id) &&
                    t.Body == _selectedFlight.DestinationBody &&
                    t.Biome == _selectedFlight.DestinationBiome)
                .ToList();
            var terminals = new List<ArrivalMetadata>();
            foreach (var wolfTerminal in wolfTerminals)
            {
                var terminalModule = terminalModules
                    .FirstOrDefault(t => t.TerminalId == wolfTerminal.Id);
                var terminal = new ArrivalMetadata
                {
                    OccupiedSeats = terminalModule.part.protoModuleCrew.Count,
                    TotalSeats = terminalModule.part.CrewCapacity,
                    TerminalId = terminalModule.TerminalId,
                    VesselName = terminalModule.vessel.GetDisplayName(),
                    VesselId = terminalModule.vessel.id,
                };
                terminals.Add(terminal);
            }
            if (_passengers != null && _passengers.Count > 0 && _selectedTerminal != null)
            {
                foreach (var terminal in terminals)
                {
                    if (terminal.TerminalId == _selectedTerminal.TerminalId)
                    {
                        terminal.OccupiedSeats += _passengers.Count;
                    }
                }
            }
            return terminals
                .OrderBy(t => t.VesselName)
                    .ThenBy(t => t.TerminalId)
                .ToList();
        }

        public void Launch()
        {
            if (!_activeWarnings.Any(w => w.Value.PreventsAction))
            {
                var activeVessel = FlightGlobals.ActiveVessel;
                var landedSituations = Vessel.Situations.LANDED |
                    Vessel.Situations.PRELAUNCH |
                    Vessel.Situations.SPLASHED;
                var landedOnly = (activeVessel.situation & landedSituations) == landedSituations;
                var vessels = LogisticsTools
                    .GetNearbyVessels(TERMINAL_RANGE, true, activeVessel, landedOnly);
                var passengers = _passengers.Values.Select(p => p.Name);
                foreach (var vessel in vessels)
                {
                    var crew = vessel.GetVesselCrew().ToArray();
                    foreach (var kerbal in crew)
                    {
                        if (passengers.Contains(kerbal.name))
                        {
                            if (_selectedFlight.Embark(new Passenger(kerbal)))
                            {
                                vessel.CrewListSetDirty();
                                var parts = vessel.parts;
                                foreach (var part in parts)
                                {
                                    if (part.protoModuleCrew.Contains(kerbal))
                                    {
                                        part.RemoveCrewmember(kerbal);
                                        break;
                                    }
                                }
                                kerbal.rosterStatus = ProtoCrewMember.RosterStatus.Missing;
                                kerbal.SetTimeForRespawn(double.MaxValue);
                            }
                            else
                            {
                                ShowWarning("Could not launch.", false);
                                return;
                            }
                        }
                    }
                }
                _selectedFlight.Launch(Planetarium.GetUniversalTime());
                _passengers.Clear();
                _activeWarnings.Clear();
                var metadata = new FlightMetadata()
                {
                    ArrivalBiome = _selectedFlight.DestinationBiome,
                    ArrivalBody = _selectedFlight.DestinationBody,
                    DepartureBiome = _selectedFlight.OriginBiome,
                    DepartureBody = _selectedFlight.OriginBody,
                    Duration = GetDuration(_selectedFlight),
                    EconomySeats = GetEconomySeats(_selectedFlight),
                    FlightNumber = _selectedFlight.FlightNumber,
                    LuxurySeats = GetLuxurySeats(_selectedFlight),
                    Status = _selectedFlight.FlightStatus,
                    UniqueId = _selectedFlight.UniqueId,
                };
                _window.Launched(metadata);
            }
        }

        private void LazyUpdate()
        {
            if (_window.gameObject.activeSelf)
            {
                // Get metadata from WOLF
                if (_wolf != null)
                {
                    var flights = _wolf.GetCrewRoutes(Planetarium.GetUniversalTime());
                    var metadata = flights
                        .OrderBy(f => f.OriginBody)
                            .ThenBy(f => f.DestinationBody)
                            .ThenBy(f => f.OriginBiome)
                            .ThenBy(f => f.DestinationBiome)
                        .Select(f => new FlightMetadata
                        {
                            ArrivalBiome = f.DestinationBiome,
                            ArrivalBody = f.DestinationBody,
                            DepartureBiome = f.OriginBiome,
                            DepartureBody = f.OriginBody,
                            Duration = GetDuration(f),
                            EconomySeats = GetEconomySeats(f),
                            FlightNumber = f.FlightNumber,
                            LuxurySeats = GetLuxurySeats(f),
                            Status = f.FlightStatus,
                            UniqueId = f.UniqueId,
                        })
                        .ToList();
                    _window.ShowFlights(metadata);
                }
                if (_selectedFlight != null)
                {
                    if (_selectedFlight.FlightStatus == FlightStatus.Boarding)
                    {
                        if (HighLogic.LoadedSceneIsFlight)
                        {
                            var kerbals = GetNearbyKerbals();
                            if (kerbals == null || kerbals.Count < 1)
                            {
                                _activeWarnings.Clear();
                                _passengers.Clear();
                                _window.ShowPassengers(null);
                                ShowWarning(SelectedFlightPanelLabels.NoKerbalsToBoardMessage, true);
                            }
                            else
                            {
                                ClearWarning(SelectedFlightPanelLabels.NoKerbalsToBoardMessage);
                                var passengers = kerbals
                                    .Select(k => new PassengerMetadata
                                    {
                                        Name = k.name,
                                        DisplayName = k.displayName,
                                        Occupation = k.experienceTrait.Title,
                                        IsTourist = k.type == ProtoCrewMember.KerbalType.Tourist,
                                        Stars = k.experienceLevel,
                                    })
                                    .OrderBy(p => p.DisplayName)
                                    .ToList();
                                _window.ShowPassengers(passengers);
                            }
                        }
                        else
                        {
                            _window.ShowAlert(SelectedFlightPanelLabels.NoKerbalsToBoardMessage);
                        }
                    }
                    else
                    {
                        var passengers = _selectedFlight.Passengers
                            .Select(p => new PassengerMetadata
                            {
                                Name = p.Name,
                                DisplayName = p.DisplayName,
                                Occupation = p.Occupation,
                                IsTourist = p.IsTourist,
                                Stars = p.Stars,
                            })
                            .OrderBy(p => p.DisplayName)
                            .ToList();
                        _window.ShowPassengers(passengers);
                        if (_selectedFlight.FlightStatus == FlightStatus.Arrived)
                        {
                            if (!HighLogic.LoadedSceneIsFlight)
                            {
                                _window.ShowAlert(_arrivalWarnings.NoNearbyTerminalsMessage);
                                ShowWarning(_arrivalWarnings.NoNearbyTerminalsMessage, true);
                            }
                            else
                            {
                                var terminals = GetNearbyTerminals();
                                if (terminals == null || terminals.Count < 1)
                                {
                                    _window.ShowAlert(_arrivalWarnings.NoNearbyTerminalsMessage);
                                    ShowWarning(_arrivalWarnings.NoNearbyTerminalsMessage, true);
                                }
                                else
                                {
                                    _window.HideAlert();
                                    ClearWarning(_arrivalWarnings.NoNearbyTerminalsMessage);
                                    if (_selectedTerminal == null)
                                    {
                                        ShowWarning(_arrivalWarnings.NoTerminalSelectedMessage, true);
                                    }
                                    else
                                    {
                                        ClearWarning(_arrivalWarnings.NoTerminalSelectedMessage);
                                    }
                                }
                                _window.ShowArrivalTerminals(terminals);
                            }
                        }
                    }
                }
            }
        }

        public override void OnAwake()
        {
            base.OnAwake();

            GetLocalizedTextValues();

            var wolfScenario = FindObjectOfType<WOLF_ScenarioModule>();
            _wolf = wolfScenario.ServiceManager.GetService<IRegistryCollection>();

            var usiTools = USI_AddonServiceManager.Instance;
            if (usiTools != null)
            {
                var serviceManager = usiTools.ServiceManager;
                var windowManager = serviceManager.GetService<WindowManager>();

                try
                {
                    // Setup UI prefabs
                    var filepath = Path.Combine(KSPUtil.ApplicationRootPath,
                        "GameData/UmbraSpaceIndustries/WOLF/Assets/UI/CrewTransferWindow.prefabs");
                    var prefabs = AssetBundle.LoadFromFile(filepath);
                    var flightSelectorPrefab = prefabs.LoadAsset<GameObject>("FlightSelector");
                    var kerbalSelectorPrefab = prefabs.LoadAsset<GameObject>("KerbalSelector");
                    var terminalWindowPrefab = prefabs.LoadAsset<GameObject>("TerminalWindow");
                    var warningPanelPrefab = prefabs.LoadAsset<GameObject>("WarningPanel");

                    // Register prefabs with window manager
                    windowManager
                        .RegisterPrefab<FlightSelector>(flightSelectorPrefab)
                        .RegisterPrefab<KerbalSelector>(kerbalSelectorPrefab)
                        .RegisterPrefab<WarningPanel>(warningPanelPrefab)
                        .RegisterWindow<TerminalWindow>(terminalWindowPrefab);
                }
                catch (ServiceAlreadyRegisteredException) { }
                catch (NullReferenceException)
                {
                    // TODO - Create an asset bundle loader service in USITools
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WOLF] {ClassName}: {ex.Message}");
                }

                try
                {
                    _window = windowManager.GetWindow<TerminalWindow>();
                    _window.Initialize(this, windowManager, () =>
                    {
                        if (_toolbarButton != null)
                        {
                            _toolbarButton.SetFalse(false);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WOLF] {ClassName}: {ex.Message}");
                    enabled = false;
                    return;
                }

                // Create toolbar button
                var textureService = serviceManager.GetService<TextureService>();
                var toolbarIcon = textureService.GetTexture(
                    "GameData/UmbraSpaceIndustries/WOLF/Assets/UI/crew-transfers.png", 36, 36);
                var showInScenes = ApplicationLauncher.AppScenes.FLIGHT |
                    ApplicationLauncher.AppScenes.MAPVIEW |
                    ApplicationLauncher.AppScenes.SPACECENTER |
                    ApplicationLauncher.AppScenes.SPH |
                    ApplicationLauncher.AppScenes.TRACKSTATION |
                    ApplicationLauncher.AppScenes.VAB;
                _toolbarButton = ApplicationLauncher.Instance.AddModApplication(
                    ShowWindow,
                    CloseWindow,
                    null,
                    null,
                    null,
                    null,
                    showInScenes,
                    toolbarIcon);
            }
        }

        /// <summary>
        /// Implementation of <see cref="MonoBehaviour"/>.OnDestroy() method.
        /// </summary>
        [SuppressMessage("CodeQuality",
            "IDE0051:Remove unused private members",
            Justification = "Because MonoBehaviour")]
        private void OnDestroy()
        {
            _window.Reset();
            if (_toolbarButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(_toolbarButton);
                _toolbarButton = null;
            }
        }

        public void PassengerSelected(PassengerMetadata passenger, bool isSelected)
        {
            if (isSelected && !_passengers.ContainsKey(passenger.Name))
            {
                _passengers.Add(passenger.Name, passenger);
            }
            else if (!isSelected && _passengers.ContainsKey(passenger.Name))
            {
                _passengers.Remove(passenger.Name);
            }
            CheckBerths();
        }

        public void ShowWarning(string message, bool preventAction)
        {
            if (!_activeWarnings.ContainsKey(message))
            {
                _activeWarnings.Add(message, new WarningMetadata(message, preventAction));
            }
            _window.ShowWarnings(_activeWarnings.Values.ToList());
        }

        public void ShowWindow()
        {
            if (_window != null)
            {
                _window.ShowWindow();
            }
        }

        public void ShowWindow(string body, string biome)
        {
            if (_window != null)
            {
                _window.ShowWindow(body, biome);
            }
        }

        /// <summary>
        /// Implementation of the <see cref="MonoBehaviour"/>.Update() method.
        /// </summary>
        [SuppressMessage(
            "CodeQuality",
            "IDE0051:Remove unused private members",
            Justification = "Because MonoBehaviour")]
        private void Update()
        {
            if (Planetarium.GetUniversalTime() >= _nextLazyUpdate)
            {
                LazyUpdate();
                _nextLazyUpdate = Planetarium.GetUniversalTime() + 0.1d;
            }
        }
    }
}
