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
    [Gui.CategoryOrder("Trade Signals", 5)]
    [Gui.CategoryOrder("Vertical Signal Bars", 6)]
    public class HALRSIv3Optimized : Indicator
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
        
        // Optimization: Pre-calculated values
        private double _signalOffsetValue;
        private double _brushOpacity;
        #endregion

        #region Indicator methods
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                                 = @"Optimized Laguerre RSI with Simple Price Direction Signals.";
                Name                                        = "HALRSIv3";
                Calculate                                   = Calculate.OnPriceChange;
                IsOverlay                                   = false;
                DisplayInDataBox                            = true;
                DrawOnPricePanel                            = true;
                DrawHorizontalGridLines                     = true;
                DrawVerticalGridLines                       = true;
                PaintPriceMarkers                           = true;
                ScaleJustification                          = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive                    = true;
                
                // RSI settings
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
                
                // Signal properties
                DrawSignals                                 = true;
                LongOn                                      = "LongLRSI";
                ShortOn                                     = "ShortLRSI";
                LongSignalBrush                             = Brushes.LimeGreen;
                ShortSignalBrush                            = Brushes.Red;
                _signalOffset                               = 5;
                
                // Vertical bar properties
                DrawVerticalBars                            = true;
                LongBarBrush                                = Brushes.LimeGreen;
                ShortBarBrush                               = Brushes.Red;
                _barOpacity                                 = 50;
                
                AddPlot(new Stroke(Brushes.White, 2), PlotStyle.Line, "LRSI");
                AddLine(Brushes.White, 50, "Middle");
            }
            else if (State == State.Configure)
            {
                _l0Series = new Series<double>(this);
                _l1Series = new Series<double>(this);
                _l2Series = new Series<double>(this);
                _l3Series = new Series<double>(this);
                _gOSeries = new Series<double>(this);
                _gHSeries = new Series<double>(this);
                _gLSeries = new Series<double>(this);
                _gCSeries = new Series<double>(this);
                
                // Pre-calculate frequently used values
                UpdateCalculatedValues();
            }
        }

        private void UpdateCalculatedValues()
        {
            _signalOffsetValue = _signalOffset * TickSize;
            _brushOpacity = _barOpacity * 0.01;
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
            // Laguerre RSI calculation (unchanged for accuracy)
            if (!UseFractalEnergy)
            {
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
                if (CurrentBar < (NFE + 1))
                    return;

                double w = (2.0 * Math.PI / GLength);
                double beta = (1.0 - Math.Cos(w)) / (Math.Pow(1.414, 2.0 / BetaDev) - 1.0);
                double alpha = (-beta + Math.Sqrt((beta * beta + 2.0 * beta)));

                _gOSeries[0] = Math.Pow(alpha, 4.0) * Open[0] + 4.0 * (1.0 - alpha) * _gOSeries[1] - 6.0 * Math.Pow(1.0 - alpha, 2.0) * _gOSeries[2] + 4.0 * Math.Pow(1.0 - alpha, 3.0) * _gOSeries[3] - Math.Pow(1.0 - alpha, 4.0) * _gOSeries[4];
                _gHSeries[0] = Math.Pow(alpha, 4.0) * High[0] + 4.0 * (1.0 - alpha) * _gHSeries[1] - 6.0 * Math.Pow(1.0 - alpha, 2.0) * _gHSeries[2] + 4.0 * Math.Pow(1.0 - alpha, 3.0) * _gHSeries[3] - Math.Pow(1.0 - alpha, 4.0) * _gHSeries[4];
                _gLSeries[0] = Math.Pow(alpha, 4.0) * Low[0] + 4.0 * (1.0 - alpha) * _gLSeries[1] - 6.0 * Math.Pow(1.0 - alpha, 2.0) * _gLSeries[2] + 4.0 * Math.Pow(1.0 - alpha, 3.0) * _gLSeries[3] - Math.Pow(1.0 - alpha, 4.0) * _gLSeries[4];
                _gCSeries[0] = Math.Pow(alpha, 4.0) * Close[0] + 4.0 * (1.0 - alpha) * _gCSeries[1] - 6.0 * Math.Pow(1.0 - alpha, 2.0) * _gCSeries[2] + 4.0 * Math.Pow(1.0 - alpha, 3.0) * _gCSeries[3] - Math.Pow(1.0 - alpha, 4.0) * _gCSeries[4];

                double o = (_gOSeries[0] + _gCSeries[1]) * 0.5;
                double h = Math.Max(_gHSeries[0], _gCSeries[1]);
                double l = Math.Min(_gLSeries[0], _gCSeries[1]);
                double c = (o + h + l + _gCSeries[0]) * 0.25;
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

            // Optimization: Cache frequently accessed values
            double currentClose = Close[0];
            double prevClose = CurrentBar > 0 ? Close[1] : currentClose;

            // Optimized plot coloring (single comparison)
            PlotBrushes[0][0] = (currentClose > prevClose) ? Brushes.LimeGreen : Brushes.Red;

            // Early exit if signals not enabled
            if (!DrawSignals && !DrawVerticalBars)
                return;

            // Optimized signal detection
            if (CurrentBar >= 1)
            {
                // Cache RSI values to avoid multiple property access
                double currentRSI = LRSI[0];
                double prevRSI = LRSI[1];
                
                // Cache price direction
                bool priceMovingUp = currentClose > prevClose;
                bool priceMovingDown = currentClose < prevClose;

                // Long Signal: RSI crosses above oversold level AND price is moving up
                if (priceMovingUp && prevRSI <= OversoldLevel && currentRSI > OversoldLevel)
                {
                    if (DrawSignals)
                    {
                        Draw.TriangleUp(this, $"{LongOn}{CurrentBar}", true, 0, 
                            Low[0] - _signalOffsetValue, LongSignalBrush);
                    }
                    
                    if (DrawVerticalBars)
                    {
                        var longBrush = LongBarBrush.Clone();
                        longBrush.Opacity = _brushOpacity;
                        Draw.VerticalLine(this, $"LongBar{CurrentBar}", 0, longBrush, 
                            DashStyleHelper.Solid, 8, false);
                    }
                }
                // Short Signal: RSI crosses below overbought level AND price is moving down
                else if (priceMovingDown && prevRSI >= OverboughtLevel && currentRSI < OverboughtLevel)
                {
                    if (DrawSignals)
                    {
                        Draw.TriangleDown(this, $"{ShortOn}{CurrentBar}", true, 0, 
                            High[0] + _signalOffsetValue, ShortSignalBrush);
                    }
                    
                    if (DrawVerticalBars)
                    {
                        var shortBrush = ShortBarBrush.Clone();
                        shortBrush.Opacity = _brushOpacity;
                        Draw.VerticalLine(this, $"ShortBar{CurrentBar}", 0, shortBrush, 
                            DashStyleHelper.Solid, 8, false);
                    }
                }
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (!IsVisible || chartControl == null || chartScale == null || 
                ChartBars == null || RenderTarget == null)
                return;

            // Cache calculations to avoid repeated calls
            var fromIndex = Math.Max(ChartBars.FromIndex - 1, 0);
            var leftX = chartControl.GetXByBarIndex(ChartBars, fromIndex);
            var rightX = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex);
            var width = rightX - leftX;

            // Draw overbought region
            if (_overboughtBrushDx?.IsDisposed == false && OverboughtRegionBrush != Brushes.Transparent)
            {
                var topY = chartScale.GetYByValue(100);
                var bottomY = chartScale.GetYByValue(OverboughtLevel);
                var rect = new SharpDX.RectangleF(leftX, topY, width, bottomY - topY);
                RenderTarget.FillRectangle(rect, _overboughtBrushDx);
            }

            // Draw oversold region
            if (_oversoldBrushDx?.IsDisposed == false && OversoldRegionBrush != Brushes.Transparent)
            {
                var topY = chartScale.GetYByValue(OversoldLevel);
                var bottomY = chartScale.GetYByValue(0);
                var rect = new SharpDX.RectangleF(leftX, topY, width, bottomY - topY);
                RenderTarget.FillRectangle(rect, _oversoldBrushDx);
            }

            base.OnRender(chartControl, chartScale);
        }

        public override void OnRenderTargetChanged()
        {
            _overboughtBrushDx?.Dispose();
            _oversoldBrushDx?.Dispose();

            if (RenderTarget != null)
            {
                try
                {
                    float opacity = (float)(RegionOpacity * 0.01f);
                    _overboughtBrushDx = OverboughtRegionBrush.ToDxBrush(RenderTarget, opacity);
                    _oversoldBrushDx = OversoldRegionBrush.ToDxBrush(RenderTarget, opacity);
                }
                catch { /* Silent catch for disposal safety */ }
            }
        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name="UseFractalEnergy", Description="Toggles the use of the Fractal Energy calculation.", Order=1, GroupName="Parameters")]
        public bool UseFractalEnergy { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name="Alpha", Order=1, GroupName= "Laguerre RSI")]
        public double Alpha { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="NFE", Description="Number of bars used in Fractal Energy calculations.", Order=1, GroupName= "Laguerre RSI with Fractal Energy")]
        public int NFE { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="GLength", Description="Period length for Go/Gh/Gl/Gc filter.", Order=2, GroupName= "Laguerre RSI with Fractal Energy")]
        public int GLength { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="BetaDev", Description="Controls reactivity in alpha/beta computations.", Order=3, GroupName= "Laguerre RSI with Fractal Energy")]
        public int BetaDev { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name="OverboughtLevel", Order=1, GroupName= "Thresholds")]
        public double OverboughtLevel { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name="OversoldLevel", Order=2, GroupName= "Thresholds")]
        public double OversoldLevel { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="OverboughtRegionBrush", Order=3, GroupName= "Thresholds")]
        public Brush OverboughtRegionBrush { get; set; }

        [Browsable(false)]
        public string OverboughtRegionBrushSerializable
        {
            get { return Serialize.BrushToString(OverboughtRegionBrush); }
            set { OverboughtRegionBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="OversoldRegionBrush", Order=4, GroupName= "Thresholds")]
        public Brush OversoldRegionBrush { get; set; }

        [Browsable(false)]
        public string OversoldRegionBrushSerializable
        {
            get { return Serialize.BrushToString(OversoldRegionBrush); }
            set { OversoldRegionBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Region Opacity", Description = "The opacity of the overbought/oversold regions (0 = completely transparent, 100 = no opacity).", Order = 5, GroupName = "Thresholds")]
        public int RegionOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Draw Signals", Description = "Draw triangle signals when conditions are met.", Order = 1, GroupName = "Trade Signals")]
        public bool DrawSignals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Long On", Description = "Long signal identifier", Order = 2, GroupName = "Trade Signals")]
        public string LongOn { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Long Signal Brush", Description = "Color used for long/buy signals.", Order = 3, GroupName = "Trade Signals")]
        public Brush LongSignalBrush { get; set; }

        [Browsable(false)]
        public string LongSignalBrushSerializable
        {
            get { return Serialize.BrushToString(LongSignalBrush); }
            set { LongSignalBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Short On", Description = "Short signal identifier", Order = 4, GroupName = "Trade Signals")]
        public string ShortOn { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Short Signal Brush", Description = "Color used for short/sell signals.", Order = 5, GroupName = "Trade Signals")]
        public Brush ShortSignalBrush { get; set; }

        [Browsable(false)]
        public string ShortSignalBrushSerializable
        {
            get { return Serialize.BrushToString(ShortSignalBrush); }
            set { ShortSignalBrush = Serialize.StringToBrush(value); }
        }

        private int _signalOffset;
        private int _barOpacity;
        
        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Signal Offset", Description = "Distance of the signal triangles from the price bars (in ticks).", Order = 6, GroupName = "Trade Signals")]
        public int SignalOffset 
        { 
            get { return _signalOffset; }
            set 
            { 
                _signalOffset = value;
                if (State == State.Active)
                    UpdateCalculatedValues();
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Draw Vertical Bars", Description = "Draw vertical bars on the indicator when signal conditions are met.", Order = 1, GroupName = "Vertical Signal Bars")]
        public bool DrawVerticalBars { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Long Bar Brush", Description = "Color used for long signal vertical bars.", Order = 2, GroupName = "Vertical Signal Bars")]
        public Brush LongBarBrush { get; set; }

        [Browsable(false)]
        public string LongBarBrushSerializable
        {
            get { return Serialize.BrushToString(LongBarBrush); }
            set { LongBarBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Short Bar Brush", Description = "Color used for short signal vertical bars.", Order = 3, GroupName = "Vertical Signal Bars")]
        public Brush ShortBarBrush { get; set; }

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
        { 
            get { return _barOpacity; }
            set 
            { 
                _barOpacity = value;
                if (State == State.Active)
                    UpdateCalculatedValues();
            }
        }

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
		private Myindicators.HALRSIv3Optimized[] cacheHALRSIv3Optimized;
		public Myindicators.HALRSIv3Optimized HALRSIv3Optimized(bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool drawSignals, string longOn, Brush longSignalBrush, string shortOn, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return HALRSIv3Optimized(Input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, drawSignals, longOn, longSignalBrush, shortOn, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}

		public Myindicators.HALRSIv3Optimized HALRSIv3Optimized(ISeries<double> input, bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool drawSignals, string longOn, Brush longSignalBrush, string shortOn, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			if (cacheHALRSIv3Optimized != null)
				for (int idx = 0; idx < cacheHALRSIv3Optimized.Length; idx++)
					if (cacheHALRSIv3Optimized[idx] != null && cacheHALRSIv3Optimized[idx].UseFractalEnergy == useFractalEnergy && cacheHALRSIv3Optimized[idx].Alpha == alpha && cacheHALRSIv3Optimized[idx].NFE == nFE && cacheHALRSIv3Optimized[idx].GLength == gLength && cacheHALRSIv3Optimized[idx].BetaDev == betaDev && cacheHALRSIv3Optimized[idx].OverboughtLevel == overboughtLevel && cacheHALRSIv3Optimized[idx].OversoldLevel == oversoldLevel && cacheHALRSIv3Optimized[idx].OverboughtRegionBrush == overboughtRegionBrush && cacheHALRSIv3Optimized[idx].OversoldRegionBrush == oversoldRegionBrush && cacheHALRSIv3Optimized[idx].RegionOpacity == regionOpacity && cacheHALRSIv3Optimized[idx].DrawSignals == drawSignals && cacheHALRSIv3Optimized[idx].LongOn == longOn && cacheHALRSIv3Optimized[idx].LongSignalBrush == longSignalBrush && cacheHALRSIv3Optimized[idx].ShortOn == shortOn && cacheHALRSIv3Optimized[idx].ShortSignalBrush == shortSignalBrush && cacheHALRSIv3Optimized[idx].SignalOffset == signalOffset && cacheHALRSIv3Optimized[idx].DrawVerticalBars == drawVerticalBars && cacheHALRSIv3Optimized[idx].LongBarBrush == longBarBrush && cacheHALRSIv3Optimized[idx].ShortBarBrush == shortBarBrush && cacheHALRSIv3Optimized[idx].BarOpacity == barOpacity && cacheHALRSIv3Optimized[idx].EqualsInput(input))
						return cacheHALRSIv3Optimized[idx];
			return CacheIndicator<Myindicators.HALRSIv3Optimized>(new Myindicators.HALRSIv3Optimized(){ UseFractalEnergy = useFractalEnergy, Alpha = alpha, NFE = nFE, GLength = gLength, BetaDev = betaDev, OverboughtLevel = overboughtLevel, OversoldLevel = oversoldLevel, OverboughtRegionBrush = overboughtRegionBrush, OversoldRegionBrush = oversoldRegionBrush, RegionOpacity = regionOpacity, DrawSignals = drawSignals, LongOn = longOn, LongSignalBrush = longSignalBrush, ShortOn = shortOn, ShortSignalBrush = shortSignalBrush, SignalOffset = signalOffset, DrawVerticalBars = drawVerticalBars, LongBarBrush = longBarBrush, ShortBarBrush = shortBarBrush, BarOpacity = barOpacity }, input, ref cacheHALRSIv3Optimized);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.HALRSIv3Optimized HALRSIv3Optimized(bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool drawSignals, string longOn, Brush longSignalBrush, string shortOn, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return indicator.HALRSIv3Optimized(Input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, drawSignals, longOn, longSignalBrush, shortOn, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}

		public Indicators.Myindicators.HALRSIv3Optimized HALRSIv3Optimized(ISeries<double> input , bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool drawSignals, string longOn, Brush longSignalBrush, string shortOn, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return indicator.HALRSIv3Optimized(input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, drawSignals, longOn, longSignalBrush, shortOn, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.HALRSIv3Optimized HALRSIv3Optimized(bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool drawSignals, string longOn, Brush longSignalBrush, string shortOn, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return indicator.HALRSIv3Optimized(Input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, drawSignals, longOn, longSignalBrush, shortOn, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}

		public Indicators.Myindicators.HALRSIv3Optimized HALRSIv3Optimized(ISeries<double> input , bool useFractalEnergy, double alpha, int nFE, int gLength, int betaDev, double overboughtLevel, double oversoldLevel, Brush overboughtRegionBrush, Brush oversoldRegionBrush, int regionOpacity, bool drawSignals, string longOn, Brush longSignalBrush, string shortOn, Brush shortSignalBrush, int signalOffset, bool drawVerticalBars, Brush longBarBrush, Brush shortBarBrush, int barOpacity)
		{
			return indicator.HALRSIv3Optimized(input, useFractalEnergy, alpha, nFE, gLength, betaDev, overboughtLevel, oversoldLevel, overboughtRegionBrush, oversoldRegionBrush, regionOpacity, drawSignals, longOn, longSignalBrush, shortOn, shortSignalBrush, signalOffset, drawVerticalBars, longBarBrush, shortBarBrush, barOpacity);
		}
	}
}

#endregion
