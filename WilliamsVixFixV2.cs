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

namespace NinjaTrader.NinjaScript.Indicators
{
    public class WilliamsVixFixV2 : Indicator
    {
        private Series<double> wvfSeries;
        private Series<bool> isAboveUpperBandSeries;
        private Series<bool> isBelowLowerBandSeries;
        private Series<bool> isSimpleEntrySeries; // Added for tracking simple entries
        private Series<bool> isFilteredEntrySeries; // Added for tracking filtered entries

        // Declare variables for price action strength and lookback periods
        private int str = 3; // Entry price action strength
        private int ltLB = 40; // Long-term lookback period
        private int mtLB = 14; // Medium-term lookback period

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"WilliamsVixFixV2 by ChrisMoody ";
                Name = "WilliamsVixFixV2";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Inputs for WVF calculation
                SDHigh = 22;
                BBLength = 20;
                SDUp = 2;
                PHigh = 50;
                HP = 0.85;
                LP = 1.01;

                // Inputs for bar coloring
                HighlightFuchsia = true;
                HighlightWhite = true;

                // Add a plot for the WVF histogram
                AddPlot(new Stroke(Brushes.Lime, 2), PlotStyle.Bar, "VixFix");
            }
            else if (State == State.Configure)
            {
                // Initialize series for tracking previous conditions
                isAboveUpperBandSeries = new Series<bool>(this);
                isBelowLowerBandSeries = new Series<bool>(this);
                isSimpleEntrySeries = new Series<bool>(this); // Initialize simple entry series
                isFilteredEntrySeries = new Series<bool>(this); // Initialize filtered entry series
            }
            else if (State == State.DataLoaded)
            {
                // Initialize the wvf series
                wvfSeries = new Series<double>(this);
            }
        }

protected override void OnBarUpdate()
{
    // Ensure we have enough bars for the indicator
    if (CurrentBar < PHigh || CurrentBar < SDHigh) return;

    // Calculate WVF
    wvfSeries[0] = ((MAX(Close, SDHigh)[0] - Low[0]) / MAX(Close, SDHigh)[0]) * 100;

    // Calculate Bollinger Bands and Range High/Low
    double sDev = SDUp * StdDev(wvfSeries, BBLength)[0];
    double midLine = SMA(wvfSeries, BBLength)[0];
    double lowerBand = midLine - sDev;
    double upperBand = midLine + sDev;
    double rangeHigh = HP * MAX(wvfSeries, PHigh)[0];
    double rangeLow = LP * MIN(wvfSeries, PHigh)[0];

    // Store whether WVF is above upper band/range high
    bool isWvfHigh = wvfSeries[0] >= upperBand || wvfSeries[0] >= rangeHigh;
    bool wasWvfHigh = wvfSeries[1] >= upperBand || wvfSeries[1] >= rangeHigh;
    
    // Store current conditions in series for next bar
    isAboveUpperBandSeries[0] = isWvfHigh;
    isBelowLowerBandSeries[0] = wvfSeries[0] <= lowerBand || wvfSeries[0] <= rangeLow;

    // Set the plot value for the WVF histogram
    Values[0][0] = wvfSeries[0];

    // Apply color to the WVF histogram (indicator bars)
    PlotBrushes[0][0] = isWvfHigh ? Brushes.Lime : Brushes.Gray;

    // Reset entry flags
    isSimpleEntrySeries[0] = false;
    isFilteredEntrySeries[0] = false;
    
    // Price action criteria - matching .pin logic more closely
    bool upRange = Low[0] > Low[1] && Close[0] > High[1];
    bool strengthCriteria = Close[0] > Close[str] && (Close[0] < Close[ltLB] || Close[0] < Close[mtLB]);
    
    // Simple Entry - Fuchsia (bFiltered in .pin)
    bool isSimpleEntry = false;
    if (wasWvfHigh && !isWvfHigh) // WVF crossed below upperBand/rangeHigh
    {
        isSimpleEntry = true;
    }
    
    // Filtered Entry - White (.pin alert3 logic)
    bool isFilteredEntry = false;
    if (wasWvfHigh && !isWvfHigh && upRange && strengthCriteria)
    {
        isFilteredEntry = true;
    }

    // Apply colors - prioritize Filtered (White) over Simple (Fuchsia)
    if (HighlightWhite && isFilteredEntry && !isFilteredEntrySeries[1] && !isSimpleEntrySeries[1])
    {
        BarBrushes[0] = Brushes.White;
        isFilteredEntrySeries[0] = true;
    }
    else if (HighlightFuchsia && isSimpleEntry && !isSimpleEntrySeries[1] && !isFilteredEntrySeries[1])
    {
        BarBrushes[0] = Brushes.Fuchsia;
        isSimpleEntrySeries[0] = true;
    }
    else
    {
        BarBrushes[0] = null; // Default chart color
    }
}

        #region Properties

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "SDHigh", Description = "LookBack Period Standard Deviation High", Order = 1, GroupName = "Parameters")]
        public int SDHigh { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "BBLength", Description = "Bollinger Band Length", Order = 2, GroupName = "Parameters")]
        public int BBLength { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "SDUp", Description = "Bollinger Band Standard Deviation Up", Order = 3, GroupName = "Parameters")]
        public int SDUp { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "PHigh", Description = "LookBack Period Percentile High", Order = 4, GroupName = "Parameters")]
        public int PHigh { get; set; }

        [NinjaScriptProperty]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "HP", Description = "Highest Percentile: 0.90 = 90%, 0.95=95%, 0.99=99%", Order = 5, GroupName = "Parameters")]
        public double HP { get; set; }

        [NinjaScriptProperty]
        [Range(1.01, double.MaxValue)]
        [Display(Name = "LP", Description = "Lowest Percentile: 1.10 = 90%, 1.05 = 95%, 1.01 = 99%", Order = 6, GroupName = "Parameters")]
        public double LP { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Highlight Fuchsia", Description = "Highlight bars in Fuchsia for simple entry signals", Order = 7, GroupName = "Bar Coloring")]
        public bool HighlightFuchsia { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Highlight White", Description = "Highlight bars in White for filtered entry signals", Order = 8, GroupName = "Bar Coloring")]
        public bool HighlightWhite { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> VixFix
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
		private WilliamsVixFixV2[] cacheWilliamsVixFixV2;
		public WilliamsVixFixV2 WilliamsVixFixV2(int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return WilliamsVixFixV2(Input, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}

		public WilliamsVixFixV2 WilliamsVixFixV2(ISeries<double> input, int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			if (cacheWilliamsVixFixV2 != null)
				for (int idx = 0; idx < cacheWilliamsVixFixV2.Length; idx++)
					if (cacheWilliamsVixFixV2[idx] != null && cacheWilliamsVixFixV2[idx].SDHigh == sDHigh && cacheWilliamsVixFixV2[idx].BBLength == bBLength && cacheWilliamsVixFixV2[idx].SDUp == sDUp && cacheWilliamsVixFixV2[idx].PHigh == pHigh && cacheWilliamsVixFixV2[idx].HP == hP && cacheWilliamsVixFixV2[idx].LP == lP && cacheWilliamsVixFixV2[idx].HighlightFuchsia == highlightFuchsia && cacheWilliamsVixFixV2[idx].HighlightWhite == highlightWhite && cacheWilliamsVixFixV2[idx].EqualsInput(input))
						return cacheWilliamsVixFixV2[idx];
			return CacheIndicator<WilliamsVixFixV2>(new WilliamsVixFixV2(){ SDHigh = sDHigh, BBLength = bBLength, SDUp = sDUp, PHigh = pHigh, HP = hP, LP = lP, HighlightFuchsia = highlightFuchsia, HighlightWhite = highlightWhite }, input, ref cacheWilliamsVixFixV2);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.WilliamsVixFixV2 WilliamsVixFixV2(int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return indicator.WilliamsVixFixV2(Input, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}

		public Indicators.WilliamsVixFixV2 WilliamsVixFixV2(ISeries<double> input , int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return indicator.WilliamsVixFixV2(input, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.WilliamsVixFixV2 WilliamsVixFixV2(int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return indicator.WilliamsVixFixV2(Input, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}

		public Indicators.WilliamsVixFixV2 WilliamsVixFixV2(ISeries<double> input , int sDHigh, int bBLength, int sDUp, int pHigh, double hP, double lP, bool highlightFuchsia, bool highlightWhite)
		{
			return indicator.WilliamsVixFixV2(input, sDHigh, bBLength, sDUp, pHigh, hP, lP, highlightFuchsia, highlightWhite);
		}
	}
}

#endregion
