using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;

using EnumType = NinjaTrader.NinjaScript.Indicators.TOP_Market_Energy_BuyingSelling.EnumType;

namespace NinjaTrader.NinjaScript.Indicators.Myindicators
{
    public class MELineFilter : Indicator
    {
        private TOP_Market_Energy_BuyingSelling meIndicator;
        
        private int currentState = 0; // 0 = no state, 1 = bullish (green winning), -1 = bearish (red winning)
        private bool hasSignaledInState = false;
        private int signalCount = 0;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Market Energy signals filtered by line threshold and angle";
                Name = "MELineFilter";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                
                // Signal quality filters
                ThresholdLevel = 100.0;
                MinimumAngle = 45.0;
                AngleBars = 3;
                
                // Signal settings (using MTFEnergySignals pattern)
                ShowEntrySignals = true;
                LongOn = "LongEntry";
                LongEntryColor = Brushes.Lime;
                ShortOn = "ShortEntry";
                ShortEntryColor = Brushes.Red;
                SignalOffset = 5;
                
                // ME Indicator settings
                MELookback = 5;
                METhreshold = 100;
            }
            else if (State == State.DataLoaded)
            {
                meIndicator = TOP_Market_Energy_BuyingSelling(Input, EnumType.One, MELookback, METhreshold);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(AngleBars, 10))
                return;

            // Determine current cross state
            int newState = GetCrossState();
            
            // Check if state changed (reset condition)
            if (newState != currentState)
            {
                currentState = newState;
                hasSignaledInState = false; // Reset signal flag for new state
            }
            
            // Only check for signals if we're in a crossed state and haven't signaled yet
            if (currentState != 0 && !hasSignaledInState)
            {
                if (IsQualitySignal())
                {
                    // Draw signals using MTFEnergySignals pattern
                    if (ShowEntrySignals)
                    {
                        bool isBullBar = Close[0] > Open[0];
                        
                        // Draw bullish signal on bull bars
                        if (currentState == 1 && isBullBar)
                        {
                            signalCount++;
                            Draw.ArrowUp(this, LongOn + signalCount.ToString(), true, 0, 
                                       Low[0] - SignalOffset * TickSize, LongEntryColor);
                            hasSignaledInState = true; // Mark that we've signaled in this state
                        }
                        // Draw bearish signal on bear bars
                        else if (currentState == -1 && !isBullBar)
                        {
                            signalCount++;
                            Draw.ArrowDown(this, ShortOn + signalCount.ToString(), true, 0, 
                                         High[0] + SignalOffset * TickSize, ShortEntryColor);
                            hasSignaledInState = true; // Mark that we've signaled in this state
                        }
                    }
                }
            }
        }

        private int GetCrossState()
        {
            try
            {
                double upValue = meIndicator.Up[0];
                double dnValue = meIndicator.Dn[0];
                
                if (upValue > dnValue)
                    return 1; // Bullish state - green winning
                else if (dnValue > upValue)
                    return -1; // Bearish state - red winning
                else
                    return 0; // No clear state
            }
            catch
            {
                return 0;
            }
        }

        private bool IsQualitySignal()
        {
            try
            {
                bool thresholdMet = false;
                bool angleMet = false;
                
                if (currentState == 1) // Bullish state - check green line
                {
                    double currentUp = meIndicator.Up[0];
                    
                    // Check threshold rule
                    thresholdMet = currentUp >= ThresholdLevel;
                    
                    // Check angle rule using NinjaTrader's Slope function
                    double greenSlope = Slope(meIndicator.Up, 0, AngleBars - 1);
                    double greenAngleInDegrees = Math.Atan(greenSlope) * 180.0 / Math.PI;
                    angleMet = Math.Abs(greenAngleInDegrees) >= MinimumAngle;
                }
                else if (currentState == -1) // Bearish state - check red line
                {
                    double currentDn = meIndicator.Dn[0];
                    
                    // Check threshold rule
                    thresholdMet = currentDn >= ThresholdLevel;
                    
                    // Check angle rule using NinjaTrader's Slope function
                    double redSlope = Slope(meIndicator.Dn, 0, AngleBars - 1);
                    double redAngleInDegrees = Math.Atan(redSlope) * 180.0 / Math.PI;
                    angleMet = Math.Abs(redAngleInDegrees) >= MinimumAngle;
                }
                
                // Signal is valid if BOTH conditions are met
                return thresholdMet && angleMet;
            }
            catch
            {
                return false;
            }
        }

        #region Properties
        
        [NinjaScriptProperty]
        [Range(50.0, 200.0)]
        [Display(Name = "Threshold Level", Description = "Winning line must cross above this level", Order = 1, GroupName = "Quality Filters")]
        public double ThresholdLevel { get; set; }

        [NinjaScriptProperty]
        [Range(15.0, 90.0)]
        [Display(Name = "Minimum Angle", Description = "Minimum winning line angle in degrees", Order = 2, GroupName = "Quality Filters")]
        public double MinimumAngle { get; set; }

        [NinjaScriptProperty]
        [Range(2, 5)]
        [Display(Name = "Angle Calculation Bars", Description = "Number of bars to calculate angle", Order = 3, GroupName = "Quality Filters")]
        public int AngleBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Entry Signals", Order = 1, GroupName = "Entry Signal Settings")]
        public bool ShowEntrySignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", GroupName = "Entry Signal Settings", Order = 2)]
        public string LongOn { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Long Entry Color", Description = "Color for the long entry arrow", Order = 3, GroupName = "Entry Signal Settings")]
        public Brush LongEntryColor { get; set; }

        [Browsable(false)]
        public string LongEntryColorSerializable
        {
            get { return Serialize.BrushToString(LongEntryColor); }
            set { LongEntryColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Short On", GroupName = "Entry Signal Settings", Order = 4)]
        public string ShortOn { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Short Entry Color", Description = "Color for the short entry arrow", Order = 5, GroupName = "Entry Signal Settings")]
        public Brush ShortEntryColor { get; set; }

        [Browsable(false)]
        public string ShortEntryColorSerializable
        {
            get { return Serialize.BrushToString(ShortEntryColor); }
            set { ShortEntryColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Signal Offset", GroupName = "Entry Signal Settings", Order = 6)]
        public double SignalOffset { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "ME Lookback", Description = "Market Energy lookback parameter", Order = 1, GroupName = "ME Settings")]
        public int MELookback { get; set; }

        [NinjaScriptProperty]
        [Range(50, 200)]
        [Display(Name = "ME Threshold", Description = "Market Energy threshold parameter", Order = 2, GroupName = "ME Settings")]
        public int METhreshold { get; set; }

        #endregion
    }
}
