#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
    [Gui.CategoryOrder("Parameters", 1)]
    [Gui.CategoryOrder("Laguerre RSI", 2)]
    [Gui.CategoryOrder("Laguerre RSI with Fractal Energy", 3)]
    [Gui.CategoryOrder("Thresholds", 4)]
    [Gui.CategoryOrder("Alerts", 5)]
    [Gui.CategoryOrder("Trade Signals", 6)]
    [Gui.CategoryOrder("Vertical Signal Bars", 7)]
    public class HALRSIv2 : Indicator
    {
        #region Members
        private Series<double> _l0Series;
        private Series<double> _l1Series;
        private Series<double> _l2Series;
        private Series<double> _l3Series;
        private Series<double> _gOSeries;
        private Series<double> _gHSeries;
        private Series<double> _gLSeries;
        private Series<double> _gCSeries;
        private SharpDX.Direct2D1.Brush _overboughtBrushDx;
        private SharpDX.Direct2D1.Brush _oversoldBrushDx;
        private Series<double> _haOpenSeries;
        private Series<double> _haHighSeries;
        private Series<double> _haLowSeries;
        private Series<double> _haCloseSeries;
        
        // Signal tracking members
        private Series<int> _dotState;  // -1 = Bearish, 0 = Changing, 1 = Bullish
        private bool _lastBarWasOversold;
        private bool _lastBarWasOverbought;
        #endregion

        #region Indicator methods
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                                 = @"Laguerre RSI with Advanced Trade Signals.";
                Name                                        = "HALRSIv2";
                Calculate                                   = Calculate.OnPriceChange;
                IsOverlay                                   = false;
                DisplayInDataBox                            = true;
                DrawOnPricePanel                            = true;  // Changed to true for triangle signals
                DrawHorizontalGridLines                     = true;
                DrawVerticalGridLines                       = true;
                PaintPriceMarkers                           = true;
                ScaleJustification                          = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                
                // Original v2 settings
                UseFractalEnergy                            = true;
                Alpha                                       = 0.2;
                NFE                                         = 8;
                GLength                                     = 13;
                BetaDev                                     = 8;
                OverboughtLevel                             = 80;
                OversoldLevel                               = 20;
                OverboughtRegionBrush                       = Brushes.Red;
                OversoldRegionBrush                         = Brushes.Green;
                RegionOpacity                               = 40;
                EnableAlerts                                = false;
                AlertSoundsPath                             = DefaultAlertFilePath();
                EnterOverboughtAlert                        = "RSIOverbought.wav";
                ExitOverboughtAlert                         = "RSILeavingOverbought.wav";
                EnterOversoldAlert                          = "RSIOversold.wav";
                ExitOversoldAlert                           = "RSILeavingOversold.wav";
                
                // New signal properties
                DrawSignals                                 = true;
                LongSignalBrush                             = Brushes.LimeGreen;
                ShortSignalBrush                            = Brushes.Red;
                SignalOffset                                = 5;
                
                // Vertical bar properties
                DrawVerticalBars                            = true;
                LongBarBrush                                = Brushes.LimeGreen;
                ShortBarBrush                               = Brushes.Red;
                BarOpacity                                  = 50;
                
                AddPlot(new Stroke(Brushes.White, 2), PlotStyle.Line, "LRSI");
                AddLine(Brushes.White, 50, "Middle");
            }
            else if (State == State.Configure)
            {
                // Disable IsSuspendedWhileInactive if alerts are enabled.
                IsSuspendedWhileInactive = !EnableAlerts;

                _l0Series = new Series<double>(this);
                _l1Series = new Series<double>(this);
                _l2Series = new Series<double>(this);
                _l3Series = new Series<double>(this);
                _gOSeries = new Series<double>(this);
                _gHSeries = new Series<double>(this);
                _gLSeries = new Series<double>(this);
                _gCSeries = new Series<double>(this);
                
                // HA Configuration
                _haOpenSeries = new Series<double>(this);
                _haHighSeries = new Series<double>(this);
                _haLowSeries = new Series<double>(this);
                _haCloseSeries = new Series<double>(this);
                
                // Signal tracking configuration
                _dotState = new Series<int>(this);
                _lastBarWasOversold = false;
                _lastBarWasOverbought = false;
            }
        }

        public override string DisplayName
        {
            get
            {
                if (UseFractalEnergy)
                    return Name + "(" + UseFractalEnergy + "," + NFE + "," + GLength + "," + BetaDev + ")";
                else
                    return Name + "(" + UseFractalEnergy + "," + Alpha + ")";
            }
        }

        protected override void OnBarUpdate()
        {
            // Calculate Heiken Ashi values
            if (CurrentBar == 0)
            {
                _haOpenSeries[0] = Open[0];
                _haCloseSeries[0] = Close[0];
                _haHighSeries[0] = High[0];
                _haLowSeries[0] = Low[0];
                _dotState[0] = 0; // Initialize trend state to neutral
            }
            else
            {
                // Calculate Heiken Ashi values
                _haCloseSeries[0] = (Open[0] + High[0] + Low[0] + Close[0]) / 4.0;
                _haOpenSeries[0] = (_haOpenSeries[1] + _haCloseSeries[1]) / 2.0;
                _haHighSeries[0] = Math.Max(High[0], Math.Max(_haOpenSeries[0], _haCloseSeries[0]));
                _haLowSeries[0] = Math.Min(Low[0], Math.Min(_haOpenSeries[0], _haCloseSeries[0]));
            }

            // Original HALRSIv2 RSI calculation
            if (!UseFractalEnergy)
            {
                ////////////////////////////////////////////////////////////////////////////////
                // Laguerre RSI
                // This source code is subject to the terms of the Mozilla Public License 2.0 at https://mozilla.org/MPL/2.0/
                // Developer: John EHLERS
                ////////////////////////////////////////////////////////////////////////////////

                if (CurrentBar < 2)
                    return;

                double gamma = 1.0 - Alpha;
                _l0Series[0] = (1.0 - gamma) * Close[0] + gamma * _l0Series[1];
                _l1Series[0] = -gamma * _l0Series[0] + _l0Series[1] + gamma * _l1Series[1];
                _l2Series[0] = -gamma * _l1Series[0] + _l1Series[1] + gamma * _l2Series[1];
                _l3Series[0] = -gamma * _l2Series[0] + _l2Series[1] + gamma * _l3Series[1];
                double cu = (_l0Series[0] > _l1Series[0] ? _l0Series[0] - _l1Series[0] : 0) + (_l1Series[0] > _l2Series[0] ? _l1Series[0] - _l2Series[0] : 0) + (_l2Series[0] > _l3Series[0] ? _l2Series[0] - _l3Series[0] : 0);
                double cd = (_l0Series[0] < _l1Series[0] ? _l1Series[0] - _l0Series[0] : 0) + (_l1Series[0] < _l2Series[0] ? _l2Series[0] - _l1Series[0] : 0) + (_l2Series[0] < _l3Series[0] ? _l3Series[0] - _l2Series[0] : 0);
                double temp = (cu + cd == 0.0) ? -1.0 : cu + cd;
                LRSI[0] = 100.0 * (temp == -1 ? 0 : cu / temp);
            }
            else
            {
                ////////////////////////////////////////////////////////////////////////////////
                // Laguerre RSI with Fractal Energy
                // https://usethinkscript.com/threads/rsi-laguerre-with-fractal-energy-for-thinkorswim.116/
                ////////////////////////////////////////////////////////////////////////////////

                if (CurrentBar < (NFE + 1))
                    return;

                double w = (2.0 * Math.PI / GLength);
                double beta = (1.0 - Math.Cos(w)) / (Math.Pow(1.414, 2.0 / BetaDev) - 1.0);
                double alpha = (-beta + Math.Sqrt((beta * beta + 2.0 * beta)));

                _gOSeries[0] = Math.Pow(alpha, 4.0) * Open[0] + 4.0 * (1.0 - alpha) * _gOSeries[1] - 6.0 * Math.Pow(1.0 - alpha, 2.0) * _gOSeries[2] + 4.0 * Math.Pow(1.0 - alpha, 3.0) * _gOSeries[3] - Math.Pow(1.0 - alpha, 4.0) * _gOSeries[4];
                _gHSeries[0] = Math.Pow(alpha, 4.0) * High[0] + 4.0 * (1.0 - alpha) * _gHSeries[1] - 6.0 * Math.Pow(1.0 - alpha, 2.0) * _gHSeries[2] + 4.0 * Math.Pow(1.0 - alpha, 3.0) * _gHSeries[3] - Math.Pow(1.0 - alpha, 4.0) * _gHSeries[4];
                _gLSeries[0] = Math.Pow(alpha, 4.0) * Low[0] + 4.0 * (1.0 - alpha) * _gLSeries[1] - 6.0 * Math.Pow(1.0 - alpha, 2.0) * _gLSeries[2] + 4.0 * Math.Pow(1.0 - alpha, 3.0) * _gLSeries[3] - Math.Pow(1.0 - alpha, 4.0) * _gLSeries[4];
                _gCSeries[0] = Math.Pow(alpha, 4.0) * Close[0] + 4.0 * (1.0 - alpha) * _gCSeries[1] - 6.0 * Math.Pow(1.0 - alpha, 2.0) * _gCSeries[2] + 4.0 * Math.Pow(1.0 - alpha, 3.0) * _gCSeries[3] - Math.Pow(1.0 - alpha, 4.0) * _gCSeries[4];

                // Calculations
                double o = (_gOSeries[0] + _gCSeries[1]) / 2.0;
                double h = Math.Max(_gHSeries[0], _gCSeries[1]);
                double l = Math.Min(_gLSeries[0], _gCSeries[1]);
                double c = (o + h + l + _gCSeries[0]) / 4.0;
                double tempSum = 0.0;
                for (int idx = 0; idx < NFE; idx++)
                {
                    tempSum += (Math.Max(_gHSeries[idx], _gCSeries[idx + 1]) - Math.Min(_gLSeries[idx], _gCSeries[idx + 1]));
                }
                double gamma = (Math.Log((tempSum / (MAX(_gHSeries, NFE)[0] - MIN(_gLSeries, NFE)[0])))
                                /
                                Math.Log(NFE));

                _l0Series[0] = ((1.0 - gamma) * _gCSeries[0]) + (gamma * _l0Series[1]);
                _l1Series[0] = -gamma * _l0Series[0] + _l0Series[1] + gamma * _l1Series[1];
                _l2Series[0] = -gamma * _l1Series[0] + _l1Series[1] + gamma * _l2Series[1];
                _l3Series[0] = -gamma * _l2Series[0] + _l2Series[1] + gamma * _l3Series[1];

                double cu1 = 0.0;
                double cd1 = 0.0;
                double cu2 = 0.0;
                double cd2 = 0.0;
                double cu = 0.0;
                double cd = 0.0;

                if (_l0Series[0] >= _l1Series[0])
                {
                    cu1 = _l0Series[0] - _l1Series[0];
                    cd1 = 0.0;
                }
                else
                {
                    cd1 = _l1Series[0] - _l0Series[0];
                    cu1 = 0.0;
                }

                if (_l1Series[0] >= _l2Series[0])
                {
                    cu2 = cu1 + _l1Series[0] - _l2Series[0];
                    cd2 = cd1;
                }
                else
                {
                    cd2 = cd1 + _l2Series[0] - _l1Series[0];
                    cu2 = cu1;
                }

                if (_l2Series[0] >= _l3Series[0])
                {
                    cu = cu2 + _l2Series[0] - _l3Series[0];
                    cd = cd2;
                }
                else
                {
                    cu = cu2;
                    cd = cd2 + _l3Series[0] - _l2Series[0];
                }

                LRSI[0] = 100.0 * ((cu + cd) != 0.0 ? (cu / (cu + cd)) : 0.0);
            }

            // Set plot color based on Heiken Ashi coloring logic
            if (_haCloseSeries[0] > _haOpenSeries[0])
                PlotBrushes[0][0] = Brushes.LimeGreen;  // Bullish color
            else
                PlotBrushes[0][0] = Brushes.Red;        // Bearish color

            // Calculate trend state for signals (internal only - no visual dots)
            if (CurrentBar >= 1)
            {
                // Determine HA trend
                var currHAOpen = _haOpenSeries[0];
                var currHAClose = _haCloseSeries[0];
                var prevHAOpen = _haOpenSeries[1];
                var prevHAClose = _haCloseSeries[1];

                int currHATrend = (currHAClose >= currHAOpen) ? 1 : -1;
                int prevHATrend = (prevHAClose >= prevHAOpen) ? 1 : -1;

                // Calculate dot state (for signal logic only)
                if (currHATrend == prevHATrend && currHATrend > 0)
                {
                    _dotState[0] = 1;  // Bullish state
                }
                else if (currHATrend == prevHATrend && currHATrend < 0)
                {
                    _dotState[0] = -1;  // Bearish state
                }
                else
                {
                    _dotState[0] = 0;  // Changing/neutral state
                }
            }

            // Check for trade signals
            if (CurrentBar >= 2)
            {
                // Track the current state
                bool isOversold = LRSI[0] <= OversoldLevel;
                bool isOverbought = LRSI[0] >= OverboughtLevel;
                
                // Long Signal: CURRENT RSI is oversold AND dot state changed from 0 to 1 (Yellow to Green)
                if (isOversold && _dotState[1] == 0 && _dotState[0] == 1)
                {
                    // Draw triangle arrows on price panel if enabled
                    if (DrawSignals)
                    {
                        Draw.TriangleUp(this, "LongSignal" + CurrentBar, true, 0, Low[0] - SignalOffset * TickSize, LongSignalBrush);
                    }
                    
                    // Draw vertical bar on indicator if enabled
                    if (DrawVerticalBars)
                    {
                        // Create brush with opacity
                        var longBrush = LongBarBrush.Clone();
                        longBrush.Opacity = BarOpacity / 100.0;
                        
                        // Draw vertical line with 4px width on the indicator panel
                        Draw.VerticalLine(this, "LongBar" + CurrentBar, 0, longBrush, DashStyleHelper.Solid, 8, false);
                    }
                }
                
                // Short Signal: CURRENT RSI is overbought AND dot state changed from 0 to -1 (Yellow to Red)
                if (isOverbought && _dotState[1] == 0 && _dotState[0] == -1)
                {
                    // Draw triangle arrows on price panel if enabled
                    if (DrawSignals)
                    {
                        Draw.TriangleDown(this, "ShortSignal" + CurrentBar, true, 0, High[0] + SignalOffset * TickSize, ShortSignalBrush);
                    }
                    
                    // Draw vertical bar on indicator if enabled
                    if (DrawVerticalBars)
                    {
                        // Create brush with opacity
                        var shortBrush = ShortBarBrush.Clone();
                        shortBrush.Opacity = BarOpacity / 100.0;
                        
                        // Draw vertical line with 4px width on the indicator panel
                        Draw.VerticalLine(this, "ShortBar" + CurrentBar, 0, shortBrush, DashStyleHelper.Solid, 8, false);
                    }
                }
                
                // Update the state for the next bar
                _lastBarWasOversold = isOversold;
                _lastBarWasOverbought = isOverbought;
            }
            else if (CurrentBar > 0)
            {
                // Initialize states for first bars
                _lastBarWasOversold = LRSI[0] <= OversoldLevel;
                _lastBarWasOverbought = LRSI[0] >= OverboughtLevel;
            }

            // Original v2 alerts
            if (EnableAlerts && (State == State.Realtime) && IsFirstTickOfBar)
            {
                if (!string.IsNullOrWhiteSpace(EnterOverboughtAlert) && (LRSI[2] < OverboughtLevel) && (LRSI[1] >= OverboughtLevel))
                {
                    // Enter Overbought alert.
                    string audioFile = ResolveAlertFilePath(EnterOverboughtAlert, AlertSoundsPath);
                    Alert("EnterOverboughtAlert", Priority.High, "Laguerre RSI entered overbought region", audioFile, 10, Brushes.Black, OverboughtRegionBrush);
                }
                if (!string.IsNullOrWhiteSpace(ExitOverboughtAlert) && (LRSI[2] >= OverboughtLevel) && (LRSI[1] < OverboughtLevel))
                {
                    // Exit Overbought alert.
                    string audioFile = ResolveAlertFilePath(ExitOverboughtAlert, AlertSoundsPath);
                    Alert("ExitOverboughtAlert", Priority.High, "Laguerre RSI exited overbought region", audioFile, 10, Brushes.Black, OverboughtRegionBrush);
                }
                if (!string.IsNullOrWhiteSpace(EnterOversoldAlert) && (LRSI[2] > OversoldLevel) && (LRSI[1] <= OversoldLevel))
                {
                    // Enter Oversold alert.
                    string audioFile = ResolveAlertFilePath(EnterOversoldAlert, AlertSoundsPath);
                    Alert("EnterOversoldAlert", Priority.High, "Laguerre RSI entered oversold region", audioFile, 10, Brushes.Black, OversoldRegionBrush);
                }
                if (!string.IsNullOrWhiteSpace(ExitOversoldAlert) && (LRSI[2] <= OversoldLevel) && (LRSI[1] > OversoldLevel))
                {
                    // Exit Oversold alert.
                    string audioFile = ResolveAlertFilePath(ExitOversoldAlert, AlertSoundsPath);
                    Alert("ExitOversoldAlert", Priority.High, "Laguerre RSI exited oversold region", audioFile, 10, Brushes.Black, OversoldRegionBrush);
                }
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (!IsVisible)
                return;

            if (chartControl == null || chartScale == null || ChartBars == null || RenderTarget == null)
                return;

            var leftX = chartControl.GetXByBarIndex(ChartBars, Math.Max(ChartBars.FromIndex - 1, 0));
            var rightX = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex);
            var width = rightX - leftX;

            // Draw overbought region.
            if (OverboughtRegionBrush != null && OverboughtRegionBrush != Brushes.Transparent && _overboughtBrushDx != null && !_overboughtBrushDx.IsDisposed)
            {
                var topY = chartScale.GetYByValue(100);
                var bottomY = chartScale.GetYByValue(OverboughtLevel);
                var height = bottomY - topY;
                SharpDX.RectangleF overboughtRect = new SharpDX.RectangleF(leftX, topY, width, height);
                RenderTarget.FillRectangle(overboughtRect, _overboughtBrushDx);
            }

            // Draw oversold region.
            if (OversoldRegionBrush != null && OversoldRegionBrush != Brushes.Transparent && _oversoldBrushDx != null && !_oversoldBrushDx.IsDisposed)
            {
                var topY = chartScale.GetYByValue(OversoldLevel);
                var bottomY = chartScale.GetYByValue(0);
                var height = bottomY - topY;
                SharpDX.RectangleF oversoldRect = new SharpDX.RectangleF(leftX, topY, width, height);
                RenderTarget.FillRectangle(oversoldRect, _oversoldBrushDx);
            }

            // NOTE: Call base.OnRender as we also want the Plots to appear on the chart.
            base.OnRender(chartControl, chartScale);
        }

        public override void OnRenderTargetChanged()
        {
            if (_overboughtBrushDx != null)
                _overboughtBrushDx.Dispose();

            if (_oversoldBrushDx != null)
                _oversoldBrushDx.Dispose();

            if (RenderTarget != null)
            {
                try
                {
                    _overboughtBrushDx = OverboughtRegionBrush.ToDxBrush(RenderTarget, (float)(RegionOpacity / 100.0f));
                    _oversoldBrushDx = OversoldRegionBrush.ToDxBrush(RenderTarget, (float)(RegionOpacity / 100.0f));
                }
                catch (Exception e) { }
            }
        }
        #endregion

        // Add the new methods here
        private string DefaultAlertFilePath()
        {
            // Return the default path where alert sound files are stored in NinjaTrader
            return NinjaTrader.Core.Globals.UserDataDir + "sounds\\";
        }

        private string ResolveAlertFilePath(string filename, string directory)
        {
            if (string.IsNullOrEmpty(filename))
                return string.Empty;
            
            // Check if the filename already contains a path
            if (filename.Contains("\\") || filename.Contains("/"))
                return filename; // Return the filename as is
            
            // Make sure directory ends with a backslash
            if (!string.IsNullOrEmpty(directory) && !directory.EndsWith("\\"))
                directory += "\\";
            
            // Combine the directory and filename
            return directory + filename;
        }

        #region Properties
        [NinjaScriptProperty]
        [Display(Name="UseFractalEnergy", Description="Toggles the use of the Fractal Energy calculation.", Order=1, GroupName="Parameters")]
        public bool UseFractalEnergy
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name="Alpha", Order=1, GroupName= "Laguerre RSI")]
        public double Alpha
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="NFE", Description="Number of bars used in Fractal Energy calculations.", Order=1, GroupName= "Laguerre RSI with Fractal Energy")]
        public int NFE
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="GLength", Description="Period length for Go/Gh/Gl/Gc filter.", Order=2, GroupName= "Laguerre RSI with Fractal Energy")]
        public int GLength
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="BetaDev", Description="Controls reactivity in alpha/beta computations.", Order=3, GroupName= "Laguerre RSI with Fractal Energy")]
        public int BetaDev
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name="OverboughtLevel", Order=1, GroupName= "Thresholds")]
        public double OverboughtLevel
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name="OversoldLevel", Order=2, GroupName= "Thresholds")]
        public double OversoldLevel
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="OverboughtRegionBrush", Order=3, GroupName= "Thresholds")]
        public Brush OverboughtRegionBrush
        { get; set; }

        [Browsable(false)]
        public string OverboughtRegionBrushSerializable
        {
            get { return Serialize.BrushToString(OverboughtRegionBrush); }
            set { OverboughtRegionBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="OversoldRegionBrush", Order=4, GroupName= "Thresholds")]
        public Brush OversoldRegionBrush
        { get; set; }

        [Browsable(false)]
        public string OversoldRegionBrushSerializable
        {
            get { return Serialize.BrushToString(OversoldRegionBrush); }
            set { OversoldRegionBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Region Opacity", Description = "The opacity of the overbought/oversold regions (0 = completely transparent, 100 = no opacity).", Order = 5, GroupName = "Thresholds")]
        public int RegionOpacity
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Alerts", Description = "Trigger alerts for confirmed signals.", Order = 1, GroupName = "Alerts")]
        public bool EnableAlerts
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Alert Sounds Path", Description = "Location of alert audio files used for confirmed signals.", Order = 2, GroupName = "Alerts")]
        public string AlertSoundsPath
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enter Overbought", Description = "Alert sound used when Laguerre RSI enters the overbought region.", Order = 3, GroupName = "Alerts")]
        public string EnterOverboughtAlert
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exit Overbought", Description = "Alert sound used when Laguerre RSI exits the overbought region.", Order = 4, GroupName = "Alerts")]
        public string ExitOverboughtAlert
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enter Oversold", Description = "Alert sound used when Laguerre RSI enters the oversold region.", Order = 5, GroupName = "Alerts")]
        public string EnterOversoldAlert
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exit Oversold", Description = "Alert sound used when Laguerre RSI exits the oversold region.", Order = 6, GroupName = "Alerts")]
        public string ExitOversoldAlert
        { get; set; }

        // Trade Signal Properties
        [NinjaScriptProperty]
        [Display(Name = "Draw Signals", Description = "Draw triangle signals when conditions are met.", Order = 1, GroupName = "Trade Signals")]
        public bool DrawSignals
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Long Signal Brush", Description = "Color used for long/buy signals.", Order = 2, GroupName = "Trade Signals")]
        public Brush LongSignalBrush
        { get; set; }

        [Browsable(false)]
        public string LongSignalBrushSerializable
        {
            get { return Serialize.BrushToString(LongSignalBrush); }
            set { LongSignalBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Short Signal Brush", Description = "Color used for short/sell signals.", Order = 3, GroupName = "Trade Signals")]
        public Brush ShortSignalBrush
        { get; set; }

        [Browsable(false)]
        public string ShortSignalBrushSerializable
        {
            get { return Serialize.BrushToString(ShortSignalBrush); }
            set { ShortSignalBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Signal Offset", Description = "Distance of the signal triangles from the price bars (in ticks).", Order = 4, GroupName = "Trade Signals")]
        public int SignalOffset
        { get; set; }

        // Vertical Signal Bars Properties
        [NinjaScriptProperty]
        [Display(Name = "Draw Vertical Bars", Description = "Draw vertical bars on the indicator when signal conditions are met.", Order = 1, GroupName = "Vertical Signal Bars")]
        public bool DrawVerticalBars
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Long Bar Brush", Description = "Color used for long signal vertical bars.", Order = 2, GroupName = "Vertical Signal Bars")]
        public Brush LongBarBrush
        { get; set; }

        [Browsable(false)]
        public string LongBarBrushSerializable
        {
            get { return Serialize.BrushToString(LongBarBrush); }
            set { LongBarBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Short Bar Brush", Description = "Color used for short signal vertical bars.", Order = 3, GroupName = "Vertical Signal Bars")]
        public Brush ShortBarBrush
        { get; set; }

        [Browsable(false)]
        public string ShortBarBrushSerializable
        {
            get { return Serialize.BrushToString(ShortBarBrush); }
            set { ShortBarBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Bar Opacity", Description = "Opacity of the vertical signal bars (0 = completely transparent, 100 = completely opaque).", Order = 4, GroupName = "Vertical Signal Bars")]
        public int BarOpacity
        { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> LRSI
        {
            get { return Values[0]; }
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.HALRSIv2[] cacheHALRSIv2;
		public Myindicators.HALRSIv2 HALRSIv2(bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool enableAlerts, string alertSoundsPath, string enterOverboughtAlert, string exitOverboughtAlert, string enterOversoldAlert, string exitOversoldAlert, bool drawSignals, Brush longSignalBrush, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return HALRSIv2(Input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, enableAlerts, alertSoundsPath, enterOverboughtAlert, exitOverboughtAlert, enterOversoldAlert, exitOversoldAlert, drawSignals, longSignalBrush, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}

		public Myindicators.HALRSIv2 HALRSIv2(ISeries<double> input, bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool enableAlerts, string alertSoundsPath, string enterOverboughtAlert, string exitOverboughtAlert, string enterOversoldAlert, string exitOversoldAlert, bool drawSignals, Brush longSignalBrush, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			if (cacheHALRSIv2 != null)
				for (int idx = 0; idx < cacheHALRSIv2.Length; idx++)
					if (cacheHALRSIv2[idx] != null && cacheHALRSIv2[idx].UseFractalEnergy == useFractalEnergy && cacheHALRSIv2[idx].Alpha == alpha && cacheHALRSIv2[idx].NFE == nFE && cacheHALRSIv2[idx].GLength == gLength && cacheHALRSIv2[idx].BetaDev == betaDev && cacheHALRSIv2[idx].OverboughtLevel == overboughtLevel && cacheHALRSIv2[idx].OversoldLevel == oversoldLevel && cacheHALRSIv2[idx].OverboughtRegionBrush == overboughtRegionBrush && cacheHALRSIv2[idx].OversoldRegionBrush == oversoldRegionBrush && cacheHALRSIv2[idx].RegionOpacity == regionOpacity && cacheHALRSIv2[idx].EnableAlerts == enableAlerts && cacheHALRSIv2[idx].AlertSoundsPath == alertSoundsPath && cacheHALRSIv2[idx].EnterOverboughtAlert == enterOverboughtAlert && cacheHALRSIv2[idx].ExitOverboughtAlert == exitOverboughtAlert && cacheHALRSIv2[idx].EnterOversoldAlert == enterOversoldAlert && cacheHALRSIv2[idx].ExitOversoldAlert == exitOversoldAlert && cacheHALRSIv2[idx].DrawSignals == drawSignals && cacheHALRSIv2[idx].LongSignalBrush == longSignalBrush && cacheHALRSIv2[idx].ShortSignalBrush == shortSignalBrush && cacheHALRSIv2[idx].SignalOffset == signalOffset && cacheHALRSIv2[idx].DrawVerticalBars == drawVerticalBars && cacheHALRSIv2[idx].LongBarBrush == longBarBrush && cacheHALRSIv2[idx].ShortBarBrush == shortBarBrush && cacheHALRSIv2[idx].BarOpacity == barOpacity && cacheHALRSIv2[idx].EqualsInput(input))
						return cacheHALRSIv2[idx];
			return CacheIndicator<Myindicators.HALRSIv2>(new Myindicators.HALRSIv2(){ UseFractalEnergy = useFractalEnergy, Alpha = alpha, NFE = nFE, GLength = gLength, BetaDev = betaDev, OverboughtLevel = overboughtLevel, OversoldLevel = oversoldLevel, OverboughtRegionBrush = overboughtRegionBrush, OversoldRegionBrush = oversoldRegionBrush, RegionOpacity = regionOpacity, EnableAlerts = enableAlerts, AlertSoundsPath = alertSoundsPath, EnterOverboughtAlert = enterOverboughtAlert, ExitOverboughtAlert = exitOverboughtAlert, EnterOversoldAlert = enterOversoldAlert, ExitOversoldAlert = exitOversoldAlert, DrawSignals = drawSignals, LongSignalBrush = longSignalBrush, ShortSignalBrush = shortSignalBrush, SignalOffset = signalOffset, DrawVerticalBars = drawVerticalBars, LongBarBrush = longBarBrush, ShortBarBrush = shortBarBrush, BarOpacity = barOpacity }, input, ref cacheHALRSIv2);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.HALRSIv2 HALRSIv2(bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool enableAlerts, string alertSoundsPath, string enterOverboughtAlert, string exitOverboughtAlert, string enterOversoldAlert, string exitOversoldAlert, bool drawSignals, Brush longSignalBrush, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return indicator.HALRSIv2(Input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, enableAlerts, alertSoundsPath, enterOverboughtAlert, exitOverboughtAlert, enterOversoldAlert, exitOversoldAlert, drawSignals, longSignalBrush, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}

		public Indicators.Myindicators.HALRSIv2 HALRSIv2(ISeries<double> input , bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool enableAlerts, string alertSoundsPath, string enterOverboughtAlert, string exitOverboughtAlert, string enterOversoldAlert, string exitOversoldAlert, bool drawSignals, Brush longSignalBrush, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return indicator.HALRSIv2(input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, enableAlerts, alertSoundsPath, enterOverboughtAlert, exitOverboughtAlert, enterOversoldAlert, exitOversoldAlert, drawSignals, longSignalBrush, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.HALRSIv2 HALRSIv2(bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool enableAlerts, string alertSoundsPath, string enterOverboughtAlert, string exitOverboughtAlert, string enterOversoldAlert, string exitOversoldAlert, bool drawSignals, Brush longSignalBrush, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return indicator.HALRSIv2(Input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, enableAlerts, alertSoundsPath, enterOverboughtAlert, exitOverboughtAlert, enterOversoldAlert, exitOversoldAlert, drawSignals, longSignalBrush, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}

		public Indicators.Myindicators.HALRSIv2 HALRSIv2(ISeries<double> input , bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool enableAlerts, string alertSoundsPath, string enterOverboughtAlert, string exitOverboughtAlert, string enterOversoldAlert, string exitOversoldAlert, bool drawSignals, Brush longSignalBrush, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return indicator.HALRSIv2(input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, enableAlerts, alertSoundsPath, enterOverboughtAlert, exitOverboughtAlert, enterOversoldAlert, exitOversoldAlert, drawSignals, longSignalBrush, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}
	}
}

#endregion
