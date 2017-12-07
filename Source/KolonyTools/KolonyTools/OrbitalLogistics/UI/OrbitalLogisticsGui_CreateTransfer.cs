﻿using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace KolonyTools
{
    /// <summary>
    /// Displays UI elements for creating a new transfer.
    /// </summary>
    public class OrbitalLogisticsGui_CreateTransfer : Window
    {
        #region Local static and instance variables
        private const string TOO_FEW_VESSELS_MESSAGE =
            "This is the only vessel in the current sphere of influence that can participate in Orbital Logisitcs.";

        private ScenarioOrbitalLogistics _scenario;
        private ModuleOrbitalLogistics _module;
        private OrbitalLogisticsTransferRequest _transfer;
        private string _transferAmountText = string.Empty;
        private string _errorMessageText = string.Empty;
        private ComboBox _originVesselComboBox;
        private ComboBox _destinationVesselComboBox;
        private int _orbLogVesselsInSoI = 0;
        private int _originVesselIndex = 0;
        private int _destinationVesselIndex = 0;
        private Vessel _originVessel;
        private Vessel _destinationVessel;
        private Vector2 _availableResourceScrollViewPosition = Vector2.zero;
        private Vector2 _transferResourceScrollViewPosition = Vector2.zero;
        private List<PartResourceDefinition> _transferrableResources = new List<PartResourceDefinition>();
        private PartResourceDefinition _selectedResource;
        private List<OrbitalLogisticsResource> _originVesselResources;
        private List<OrbitalLogisticsResource> _destinationVesselResources;
        #endregion

        public OrbitalLogisticsGui_CreateTransfer(ModuleOrbitalLogistics module, ScenarioOrbitalLogistics scenario)
            : base("New Transfer", 400, 460)
        {
            _scenario = scenario;
            _module = module;

            Start();
        }

        /// <summary>
        /// Initializes the UI.
        /// </summary>
        private void Start()
        {
            // Create list of vessels in current SoI
            var vesselNames = _module.BodyVesselList
                .Select(v => new GUIContent(v.vesselName))
                .ToArray();

            _orbLogVesselsInSoI = vesselNames.Length;

            // Setup gui style for combo boxes
            GUIStyle listStyle = new GUIStyle();
            listStyle.normal.textColor = Color.white;
            listStyle.onHover.background = new Texture2D(2, 2);
            listStyle.hover.background = listStyle.onHover.background;
            listStyle.padding.left = 4;
            listStyle.padding.right = 4;
            listStyle.padding.top = 4;
            listStyle.padding.bottom = 4;

            // Create gui combo boxes for selecting source and destination vessels
            _originVesselComboBox = new ComboBox(
                rect: new Rect(20, 30, 100, 20),
                buttonContent: vesselNames[0],
                buttonStyle: "button",
                boxStyle: "box",
                listContent: vesselNames,
                listStyle: listStyle,
                onChange: i =>
                {
                    UpdateOriginVessel(i);
                }
            );
            _originVesselComboBox.SelectedItemIndex = 0;

            _destinationVesselComboBox = new ComboBox(
                rect: new Rect(20, 30, 100, 20),
                buttonContent: vesselNames[0],
                buttonStyle: "button",
                boxStyle: "box",
                listContent: vesselNames,
                listStyle: listStyle,
                onChange: i =>
                {
                    UpdateDestinationVessel(i);
                }
            );
            _destinationVesselComboBox.SelectedItemIndex = 0;

            SetVisible(true);
        }

        /// <summary>
        /// Called by <see cref="MonoBehaviour"/>.OnGUI to render the UI.
        /// </summary>
        /// <param name="windowId"></param>
        protected override void DrawWindowContents(int windowId)
        {
            // Origin vessel selection section header
            GUILayout.BeginHorizontal();
            GUILayout.Label("Origin", UIHelper.labelStyle, GUILayout.Width(80));

            // Display Previous button for origin vessel selection
            if (GUILayout.Button(UIHelper.leftArrowSymbol, UIHelper.buttonStyle, GUILayout.Width(30), GUILayout.Height(20)))
            {
                UpdateOriginVessel(GetPreviousVesselIndex(_originVesselIndex));
                _originVesselComboBox.SelectedItemIndex = _originVesselIndex;
            }

            // Display combo box for source vessel selection
            _originVesselComboBox.Show();

            // Display Next button for source vessel selection
            if (GUILayout.Button(UIHelper.rightArrowSymbol, UIHelper.buttonStyle, GUILayout.Width(30), GUILayout.Height(20)))
            {
                UpdateOriginVessel(GetNextVesselIndex(_originVesselIndex));
                _originVesselComboBox.SelectedItemIndex = _originVesselIndex;
            }
            GUILayout.EndHorizontal();  // origin selection section

            // Destination vessel selection section
            GUILayout.BeginHorizontal();
            GUILayout.Label("Destination", UIHelper.labelStyle, GUILayout.Width(80));

            // Display Previous button for destination vessel selection
            if (GUILayout.Button(UIHelper.leftArrowSymbol, UIHelper.buttonStyle, GUILayout.Width(30), GUILayout.Height(20)))
            {
                UpdateDestinationVessel(GetPreviousVesselIndex(_destinationVesselIndex));
                _destinationVesselComboBox.SelectedItemIndex = _destinationVesselIndex;
            }

            // Display combo box for destination vessel selection
            _destinationVesselComboBox.Show();

            // Display Next button destination vessel selection
            if (GUILayout.Button(UIHelper.rightArrowSymbol, UIHelper.buttonStyle, GUILayout.Width(30), GUILayout.Height(20)))
            {
                UpdateDestinationVessel(GetNextVesselIndex(_destinationVesselIndex));
                _destinationVesselComboBox.SelectedItemIndex = _destinationVesselIndex;
            }
            GUILayout.EndHorizontal();  // destination selection section

            // Create some visual separation between sections
            GUILayout.Label(string.Empty);

            // Only allow transfers between different vessels
            if (_originVesselIndex != _destinationVesselIndex)
            {
                // Display transferable resources
                _availableResourceScrollViewPosition = GUILayout.BeginScrollView(_availableResourceScrollViewPosition, GUILayout.MinHeight(130));

                // Calculate and display available amount for transferrable resources
                if (_originVessel != null && _destinationVessel != null)
                {
                    string content;
                    double available;

                    // Table header
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.Empty, UIHelper.labelStyle, GUILayout.Width(22));
                    GUILayout.Label(" Resource", UIHelper.whiteLabelStyle, GUILayout.Width(165));
                    GUILayout.Label("Available", UIHelper.whiteLabelStyle, GUILayout.MinWidth(150));
                    GUILayout.EndHorizontal();

                    // Table rows
                    foreach (var resource in _originVesselResources)
                    {
                        // Only display resources the origin and destination vessels have in common
                        if (_transferrableResources.Contains(resource.ResourceDefinition))
                        {
                            // String-ify the amount available for transfer
                            available = resource.GetAvailableAmount();

                            content = available.ToString("F0");
                            content += " / ";
                            content += resource.GetCapacity().ToString("F0");

                            // Display the table row
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button(UIHelper.downArrowSymbol, UIHelper.buttonStyle, GUILayout.Width(22), GUILayout.Height(22)))
                            {
                                _selectedResource = resource.ResourceDefinition;
                            }
                            GUILayout.Label(" " + resource.Name, UIHelper.yellowLabelStyle, GUILayout.Width(165));
                            GUILayout.Label(content, UIHelper.yellowLabelStyle, GUILayout.MinWidth(150));
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                GUILayout.EndScrollView();  // transferable resources list

                if (_selectedResource != null)
                {
                    // Calculate origin/destination resource amounts
                    var originResource = _originVesselResources
                        .Where(r => r.ResourceDefinition == _selectedResource)
                        .Single();

                    var destinationResource = _destinationVesselResources
                        .Where(r => r.ResourceDefinition == _selectedResource)
                        .Single();

                    double sourceAvailabe = originResource.GetAvailableAmount();
                    double destinationAvailable = destinationResource.GetAvailableAmount();
                    double destinationCapacity = destinationResource.GetCapacity();

                    // Show section for selected resource details and to input transfer amount
                    GUILayout.BeginHorizontal();

                    // Show section for resource details
                    GUILayout.BeginVertical();

                    // Show selected resource
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Resource:", UIHelper.whiteLabelStyle, GUILayout.Width(80));
                    GUILayout.Label(_selectedResource.name, UIHelper.yellowLabelStyle, GUILayout.Width(120));
                    GUILayout.EndHorizontal();

                    // Show origin amount
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Origin:", UIHelper.whiteLabelStyle, GUILayout.Width(80));
                    GUILayout.Label(sourceAvailabe.ToString("F1"), UIHelper.yellowLabelStyle, GUILayout.Width(120));
                    GUILayout.EndHorizontal();

                    // Show destination amount and capacity
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Destination:", UIHelper.whiteLabelStyle, GUILayout.Width(80));
                    GUILayout.Label(
                        string.Format("{0:F1} / {1:F0}", destinationAvailable, destinationCapacity),
                        UIHelper.yellowLabelStyle,
                        GUILayout.Width(120)
                    );
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();  // resource details section

                    // Show section for transfer amount
                    GUILayout.BeginVertical();

                    // Show transfer amount header
                    GUILayout.Label("Transfer Amount", UIHelper.centerAlignLabelStyle, GUILayout.Width(165));

                    // Show fill button and text input box
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Fill", UIHelper.buttonStyle, GUILayout.Width(65), GUILayout.Height(25)))
                    {
                        // Determine the amount needed fill the destination vessel with the selected resource
                        double difference = destinationCapacity - destinationAvailable;
                        if (difference > 0)
                        {
                            if (difference > sourceAvailabe)
                                difference = sourceAvailabe;

                            _transferAmountText = difference.ToString("F2");
                        }
                    }
                    _transferAmountText = GUILayout.TextField(
                        _transferAmountText, 10, UIHelper.textFieldStyle,
                        GUILayout.Width(95), GUILayout.Height(25)
                    );
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Add to Transfer", UIHelper.buttonStyle, GUILayout.Width(165)))
                    {
                        // Parse the transfer amount
                        double amount = 0;
                        if (double.TryParse(_transferAmountText, out amount))
                        {
                            if (amount > 0)
                                _transfer.AddResource(originResource, amount);
                        }
                    }

                    GUILayout.EndVertical();  // transfer amount section

                    GUILayout.EndHorizontal();  // resource details and transfer amount section
                }

                // Create some visual separation between sections
                GUILayout.Label(string.Empty);

                // Display transfer stats
                if (_transfer != null && _transfer.ResourceRequests.Count > 0)
                {
                    double totalMass = _transfer.TotalMass();
                    float totalCost = _transfer.CalculateCost();

                    _transferResourceScrollViewPosition = GUILayout.BeginScrollView(_transferResourceScrollViewPosition, GUILayout.MinHeight(130));

                    // Transfer list header
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.Empty, UIHelper.labelStyle, GUILayout.Width(22));
                    GUILayout.Label(" Resource", UIHelper.whiteLabelStyle, GUILayout.MinWidth(155));
                    GUILayout.Label("Quantity", UIHelper.whiteLabelStyle, GUILayout.Width(80));
                    GUILayout.Label("Cost", UIHelper.whiteLabelStyle, GUILayout.Width(80));
                    GUILayout.EndHorizontal();

                    // Transfer list items
                    foreach (var transfer in _transfer.ResourceRequests.ToArray())
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(UIHelper.deleteSymbol, UIHelper.buttonStyle, GUILayout.Width(22), GUILayout.Height(22)))
                        {
                            _transfer.ResourceRequests.Remove(transfer);
                            _errorMessageText = string.Empty;
                        }
                        GUILayout.Label(" " + transfer.ResourceDefinition.name, UIHelper.yellowLabelStyle, GUILayout.MinWidth(155));
                        GUILayout.Label(transfer.TransferAmount.ToString("F2"), UIHelper.yellowLabelStyle, GUILayout.Width(80));
                        GUILayout.Label((totalCost * transfer.Mass() / totalMass).ToString("F2"), UIHelper.yellowLabelStyle, GUILayout.Width(80));
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Total Mass:", UIHelper.whiteLabelStyle, GUILayout.Width(90));
                    GUILayout.Label(
                        string.Format("{0:F2} / {1:F0}", totalMass, _module.MaxTransferMass),
                        UIHelper.yellowLabelStyle, GUILayout.Width(150)
                    );
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Total Cost:", UIHelper.whiteLabelStyle, GUILayout.Width(90));
                    GUILayout.Label(totalCost.ToString("F2"), UIHelper.yellowLabelStyle, GUILayout.Width(150));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Transit Time:", UIHelper.whiteLabelStyle, GUILayout.Width(90));
                    GUILayout.Label(
                        Utilities.SecondsToKerbinTime(_transfer.Duration),
                        UIHelper.yellowLabelStyle, GUILayout.Width(150)
                    );
                    GUILayout.EndHorizontal();
                    GUILayout.Label(_errorMessageText, UIHelper.yellowLabelStyle);
                }
            }
            // Origin and destination vessel are the same, so...
            else
            {
                // Display a message if there is only one vessel in the current SoI that is eligible for OrbLog
                if (_orbLogVesselsInSoI < 2)
                {
                    GUILayout.Label(TOO_FEW_VESSELS_MESSAGE, UIHelper.yellowLabelStyle);
                }
            }

            // Display Start and Cancel buttons
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Empty, UIHelper.labelStyle, GUILayout.Width(80));
            if (_transfer != null && _transfer.ResourceRequests.Count > 0)
            {
                if (GUILayout.Button("Start Transfer", UIHelper.buttonStyle, GUILayout.Width(110)))
                {
                    if (GameSettings.VERBOSE_DEBUG_LOG)
                        Debug.Log("[MKS] OrbitalLogisiticsTransferCreateView.DrawWindowContents: Start Transfer button pressed.");

                    _errorMessageText = string.Empty;

                    if (_transfer.Launch(_scenario.PendingTransfers, out _errorMessageText))
                        ResetAndClose();
                }
            }
            else
            {
                // Add a blank label in order to "center" the Cancel button
                GUILayout.Label(string.Empty, GUILayout.Width(60));
            }
            if (GUILayout.Button("Cancel", UIHelper.buttonStyle, GUILayout.Width(100)))
            {
                ResetAndClose();
            }
            GUILayout.EndHorizontal();

            // Display the contents of the combo boxes
            _originVesselComboBox.ShowRest();
            _destinationVesselComboBox.ShowRest();
        }

        /// <summary>
        /// Change the selected origin vessel.
        /// </summary>
        /// <param name="newVesselIndex"></param>
        protected void UpdateOriginVessel(int newVesselIndex)
        {
            _errorMessageText = string.Empty;

            _selectedResource = null;
            _originVesselIndex = newVesselIndex;
            _originVessel = _module.BodyVesselList[_originVesselIndex];
            _originVesselResources = _originVessel.GetResources();

            if (_destinationVessel != null)
            {
                if (_transfer == null)
                    _transfer = new OrbitalLogisticsTransferRequest(_originVessel, _destinationVessel);
                else
                {
                    _transfer.Origin = _originVessel;
                    _transfer.ResourceRequests.Clear();
                }

                _transfer.CalculateDuration();

                _transferrableResources = OrbitalLogisticsAllowedTransfers.Instance.GetAllowedResources(_originVessel, _destinationVessel);
            }
        }

        /// <summary>
        /// Change the selected destination vessel.
        /// </summary>
        /// <param name="newVesselIndex"></param>
        protected void UpdateDestinationVessel(int newVesselIndex)
        {
            _errorMessageText = string.Empty;

            _selectedResource = null;
            _destinationVesselIndex = newVesselIndex;
            _destinationVessel = _module.BodyVesselList[_destinationVesselIndex];
            _destinationVesselResources = _destinationVessel.GetResources();

            if (_originVessel != null)
            {
                if (_transfer == null)
                    _transfer = new OrbitalLogisticsTransferRequest(_originVessel, _destinationVessel);
                else
                {
                    _transfer.Destination = _destinationVessel;
                    _transfer.ResourceRequests.Clear();
                }

                _transfer.CalculateDuration();

                _transferrableResources = OrbitalLogisticsAllowedTransfers.Instance.GetAllowedResources(_originVessel, _destinationVessel);
            }
        }

        /// <summary>
        /// Determine the index of the previous vessel in <see cref="ModuleOrbitalLogistics.BodyVesselList"/>.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        protected int GetPreviousVesselIndex(int index)
        {
            if (index == 0)
                return _module.BodyVesselList.Count() - 1;
            else
                return index - 1;
        }

        /// <summary>
        /// Determine the index of the next vessel in <see cref="ModuleOrbitalLogistics.BodyVesselList"/>.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        protected int GetNextVesselIndex(int index)
        {
            if (index == _module.BodyVesselList.Count() - 1)
                return 0;
            else
                return index + 1;
        }

        /// <summary>
        /// Clear the cache and close the window.
        /// </summary>
        protected void ResetAndClose()
        {
            _transfer = null;
            _selectedResource = null;
            _originVessel = null;
            _destinationVessel = null;
            _errorMessageText = string.Empty;

            SetVisible(false);
        }
    }
}