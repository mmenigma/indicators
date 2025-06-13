///  ZIP File Name:  MACD Custom Directional Color with Divergence Detection.zip

///  This script has code to paint price bars per MACD or Differantial Histogram conditions, but it is remarked out and not used.
///  This script has Market Analyzer functionality, but it is remarked out and not used.
///  The remaining functionality plots MACD, MACD Average, and Differential, and paints MACD line, MACD Average line, and Differential histogram per Up/Down conditions.
///  Plot order of lines plots Differential Histogram on bottom, then MACD Average, then MACD on top.
///  Added Menu to choose EMA or SMA
///  Added Divergence Detection with visual markers and lines

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
	public class MyMACDdiv : Indicator
	{
///Remove remark to restore PaintBar function.
//		public enum Macd_PaintBarType2 {macd, hist}

		private Brush UpColor;
		private Brush DownColor;
		
		// Moving average series for manual MACD calculation
		private Series<double> fastMA;
		private Series<double> slowMA;
		private Series<double> macdLine;
		private Series<double> signalLine;
		
		// Variable to store the user's selection for moving average type
		private CustomEnumNamespaceDIV.UniversalMovingAverage maType = CustomEnumNamespaceDIV.UniversalMovingAverage.EMA;
		
		// Divergence detection variables
		private List<int> priceHighs;
		private List<int> priceLows;
		private List<int> macdHighs;
		private List<int> macdLows;
		private int divergenceCounter = 0;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= "MACD (Moving Average Convergence/Divergence) with Divergence Detection is a trend following momentum indicator that shows the relationship between two moving averages of price and detects divergences.";
				Name										= "MyMACDdiv";
				Calculate									= Calculate.OnPriceChange;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= false;  ///Change to True if PaintBar funcationality enabled.
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				IsAutoScale									= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;

				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;

				Fast					= 12;
				Slow					= 26;
				Smooth					= 9;
				// Set default moving average type to EMA for backward compatibility
				ShowMACD				= false;  //True will display MACD and MACD Average lines with Up/Down directional color; False will display lines of one color.
				ShowHistogram			= true;  //True will display Differential Histogram with Up/Down directional color; False will display histogram of one color.
				ShowBackColor			= true;  //True will display chart background with Up/Down directional color; False will not change default chart background color.
				
				// Divergence settings
				ShowDivergences			= true;   //True will detect and show divergences
				DivergenceLookback		= 5;      //Bars to look back for peak/trough detection
				MinDivergenceStrength	= 3;      //Minimum bars between divergence points
				ShowRegularDiv			= true;   //Show regular divergences
				ShowHiddenDiv			= false;  //Show hidden divergences

///Remove remark to restore PaintBar function.
//				PaintBars				= false;

				HistUpRising			= Brushes.SeaGreen;
				HistUpFalling			= Brushes.DarkSeaGreen;
				HistDownFalling			= Brushes.DarkRed;
				HistDownRising			= Brushes.IndianRed;
				SignalColorRising		= Brushes.Cyan;
				SignalColorFalling		= Brushes.DarkCyan;
				MACDRising				= Brushes.Red;
				MACDFalling				= Brushes.DarkRed;
				
				// Divergence colors
				BullishDivergenceColor	= Brushes.YellowGreen;
				BearishDivergenceColor	= Brushes.IndianRed;
				HiddenBullishDivColor	= Brushes.DodgerBlue;
				HiddenBearishDivColor	= Brushes.Orange;

				/// Standard NinjaTrader chart background color is 27,27,27
				/// Initiate new solid color backbrush with custom color:  Uptrend Background Color
				UpColor = new SolidColorBrush(Color.FromRgb(40, 45, 40));  //Original:  40,40,40 (gray, with no green emphasis)
				UpColor.Freeze();
				/// Initiate new solid color backbrush with custom color:  Downtrend Background Color
				DownColor = new SolidColorBrush(Color.FromRgb(24, 14, 14));  //Original:  14,14,14 (gray, with no red emphasis)
				DownColor.Freeze();

///Remove remark to restore PaintBar function.				
//				pbType					= Macd_PaintBarType2.macd;
				
				AddPlot(Brushes.DarkGoldenrod, "Hist");  //0 - Plot order, under MACD Average and MACD
				AddPlot(Brushes.Cyan, "Avg");  //1 - Plot order, over Histogram
				AddPlot(Brushes.Red, "MacdValue");  //2 - Plot order, over MACD Average

///Remove remark to restore MarketAnalyzer function.
//				AddPlot(Brushes.Transparent, "MarketAnalyzer");  //3

				AddLine(new Stroke(Brushes.Gainsboro, DashStyleHelper.Dash, 1), 0, "ZeroLine");
				
				//Hist - MACD Differential Histogram.  Plot order - on bottom.
	            Plots[0].AutoWidth = true; 
	            Plots[0].PlotStyle = PlotStyle.Bar; 
	            Plots[0].DashStyleHelper = DashStyleHelper.Solid;
				//Avg - MACD Average (Signal).  Plot order - on top of Differential Histogram.
	            Plots[1].Width = 2; 
	            Plots[1].PlotStyle = PlotStyle.Line; 
	            Plots[1].DashStyleHelper = DashStyleHelper.Solid;
				//MacdValue - MACD.  Plot order - on top of MACD Average (Signal).
				Plots[2].Width = 2; 
	            Plots[2].PlotStyle = PlotStyle.Line; 
	            Plots[2].DashStyleHelper = DashStyleHelper.Solid;

			}
			else if (State == State.Configure)
			{
			}
			else if( State == State.DataLoaded )
			{
				// Initialize custom data series for MACD calculations
				fastMA = new Series<double>(this);
				slowMA = new Series<double>(this);
				macdLine = new Series<double>(this);
				signalLine = new Series<double>(this);
				
				// Initialize divergence tracking lists
				priceHighs = new List<int>();
				priceLows = new List<int>();
				macdHighs = new List<int>();
				macdLows = new List<int>();
			}
		}

		protected override void OnBarUpdate()
		{
            if (CurrentBar == 0)
                return;

			// Calculate moving averages based on selected type using switch
			double fastMAValue, slowMAValue;
			
			switch (maType)
			{
				case CustomEnumNamespaceDIV.UniversalMovingAverage.EMA:
					fastMAValue = EMA(Fast)[0];
					slowMAValue = EMA(Slow)[0];
					break;
				case CustomEnumNamespaceDIV.UniversalMovingAverage.SMA:
					fastMAValue = SMA(Fast)[0];
					slowMAValue = SMA(Slow)[0];
					break;
				default:
					fastMAValue = EMA(Fast)[0];
					slowMAValue = EMA(Slow)[0];
					break;
			}
			
			// Store MA values in our series
			fastMA[0] = fastMAValue;
			slowMA[0] = slowMAValue;
			
			// Calculate MACD line (Fast MA - Slow MA)
			double Macd = fastMAValue - slowMAValue;
			macdLine[0] = Macd;
			
			// Calculate Signal line (smoothed MACD)
			double Signal;
			if (CurrentBar < Smooth - 1)
			{
				Signal = 0; // Not enough data for smoothing yet
			}
			else
			{
				switch (maType)
				{
					case CustomEnumNamespaceDIV.UniversalMovingAverage.EMA:
						Signal = EMA(macdLine, Smooth)[0];
						break;
					case CustomEnumNamespaceDIV.UniversalMovingAverage.SMA:
						Signal = SMA(macdLine, Smooth)[0];
						break;
					default:
						Signal = EMA(macdLine, Smooth)[0];
						break;
				}
			}
			signalLine[0] = Signal;
			
			// Calculate Histogram (MACD - Signal)
			double Histog = Macd - Signal;

			// Set plot values
			MacdValue[0] = Macd;
			Avg[0] = Signal;
            Hist[0] = Histog;

			// Get prior values for directional comparison
            double priorMacd = CurrentBar > 0 ? MacdValue[1] : Macd;
			double priorSignal = CurrentBar > 0 ? Avg[1] : Signal;
			double priorHistog = CurrentBar > 0 ? Hist[1] : Histog;

			
///Remove remark to restore MarketAnalyzer function.
//            if (Macd >= priorMacd)
//                MarketAnalyzer[0] = -1;
//            else
//                MarketAnalyzer[0] = -2;


			//MACD and MACD Average (Signal) Up/Down Color
            if( ShowMACD)
            {
				if (Macd >= priorMacd)
                    PlotBrushes[2][0] = MACDRising;
                else
                    PlotBrushes[2][0] = MACDFalling;
				
                if (Signal >= priorSignal)
                    PlotBrushes[1][0] = SignalColorRising;
                else
                    PlotBrushes[1][0] = SignalColorFalling;
            }
            else
            {
                PlotBrushes[2][0] = Brushes.Red;
				PlotBrushes[1][0] = Brushes.Cyan;
            }

			
			//Differential Histogram Up/Down Color
			if ( ShowHistogram)
			{
            if (Histog >= 0 && Histog >= priorHistog)
                PlotBrushes[0][0] = HistUpRising;
            else if (Histog >= 0 && Histog < priorHistog)
                PlotBrushes[0][0] = HistUpFalling;

			else if (Histog < 0 && Histog >= priorHistog)
                PlotBrushes[0][0] = HistDownRising;
            else
                PlotBrushes[0][0] = HistDownFalling;
			}
            else
            {
                PlotBrushes[0][0] = Brushes.DarkGoldenrod;
            }


			//Chart Background Up/Down Color
            if( ShowBackColor)
			{
			   // Up - Background Color	
			if (Macd > Signal && Macd > 0)
				{
				BackBrush = UpColor; 
				}
			   // Down - Background Color	
			else if (Macd < Signal && Macd < 0)
				{
				BackBrush = DownColor; 
				}
			else
				{
				}
			}

			// Divergence Detection
			if (ShowDivergences && CurrentBar >= Math.Max(Fast, Slow) + Smooth + DivergenceLookback * 2)
			{
				DetectDivergences();
			}

			
///Remove remark to restore PaintBar function.			
/*			if( PaintBars 
			   && ChartControl != null )
            {
                if (pbType == Macd_PaintBarType2.macd)
                {
                    if (Macd >= 0 && Macd >= priorMacd)
                    {
                        CandleOutlineBrush = HistUpRising;
                        if (Open[0] < Close[0] 
							&& ChartBars.Properties.ChartStyleType == ChartStyleType.CandleStick )
                            BarBrush = Brushes.Transparent;
                        else
                            BarBrush = HistUpRising;
                    }
                    else if (Macd >= 0 && Macd < priorMacd)
                    {
                        CandleOutlineBrush = HistUpFalling;
                        if (Open[0] < Close[0] 
							&& ChartBars.Properties.ChartStyleType == ChartStyleType.CandleStick)
                            BarBrush = Brushes.Transparent;
                        else
                            BarBrush = HistUpFalling;
                    }
                    else if (Macd < 0 && Macd >= priorMacd)
                    {
                        CandleOutlineBrush = HistDownRising;
                        if (Open[0] < Close[0] 
							&& ChartBars.Properties.ChartStyleType == ChartStyleType.CandleStick)
                            BarBrush = Brushes.Transparent;
                        else
                            BarBrush = HistDownRising;
                    }
                    else
                    {
                        CandleOutlineBrush = HistDownFalling;
                        if (Open[0] < Close[0] 
							&& ChartBars.Properties.ChartStyleType == ChartStyleType.CandleStick)
                            BarBrush = Brushes.Transparent;
                        else
                            BarBrush = HistDownFalling;
                    }
                }
                else if (pbType == Macd_PaintBarType2.hist)
                {
                    if (Histog >= 0 && Histog >= priorHistog)
                    {
                        CandleOutlineBrush = HistUpRising;
                        if (Open[0] < Close[0] 
							&& ChartBars.Properties.ChartStyleType == ChartStyleType.CandleStick)
                            BarBrush = Brushes.Transparent;
                        else
                            BarBrush = HistUpRising;
                    }
                    else if (Histog >= 0 && Histog < priorHistog)
                    {
                        CandleOutlineBrush = HistUpFalling;
                        if (Open[0] < Close[0] 
							&& ChartBars.Properties.ChartStyleType == ChartStyleType.CandleStick)
                            BarBrush = Brushes.Transparent;
                        else
                            BarBrush = HistUpFalling;
                    }
                    else if (Histog < 0 && Histog >= priorHistog)
                    {
                        CandleOutlineBrush = HistDownRising;
                        if (Open[0] < Close[0] 
							&& ChartBars.Properties.ChartStyleType == ChartStyleType.CandleStick)
                            BarBrush = Brushes.Transparent;
                        else
                            BarBrush = HistDownRising;
                    }
                    else
                    {
                        CandleOutlineBrush = HistDownFalling;
                        if (Open[0] < Close[0] 
							&& ChartBars.Properties.ChartStyleType == ChartStyleType.CandleStick)
                            BarBrush = Brushes.Transparent;
                        else
                            BarBrush = HistDownFalling;
                    }
                }
            }			
*/
		}

		private void DetectDivergences()
		{
			// Gold standard peak-to-peak divergence detection
			int lookback = Math.Max(DivergenceLookback, 3);
			
			if (CurrentBar < lookback * 4) return;
			if (CurrentBar % 3 != 0) return; // Check every 3rd bar
			
			// Find all confirmed peaks
			List<int> priceHighBars = FindPeaks(true, true, lookback, 50);   // Price highs
			List<int> priceLowBars = FindPeaks(true, false, lookback, 50);   // Price lows
			List<int> macdHighBars = FindPeaks(false, true, lookback, 50);   // MACD highs
			List<int> macdLowBars = FindPeaks(false, false, lookback, 50);   // MACD lows
			
			// Check divergences
			if (ShowRegularDiv)
			{
				CheckDivergence(priceHighBars, macdHighBars, true, true, false);   // Bearish regular
				CheckDivergence(priceLowBars, macdLowBars, false, true, false);    // Bullish regular
			}
			
			if (ShowHiddenDiv)
			{
				CheckDivergence(priceLowBars, macdLowBars, true, false, true);     // Hidden bearish
				CheckDivergence(priceHighBars, macdHighBars, false, false, true);  // Hidden bullish
			}
		}
		
		private List<int> FindPeaks(bool isPrice, bool isHigh, int confirmBars, int maxBarsBack)
		{
			List<int> peaks = new List<int>();
			
			for (int i = confirmBars; i < Math.Min(maxBarsBack, CurrentBar); i++)
			{
				if (IsPeak(i, confirmBars, isPrice, isHigh))
					peaks.Add(i);
			}
			
			return peaks;
		}
		
		private bool IsPeak(int barsBack, int confirmBars, bool isPrice, bool isHigh)
		{
			if (CurrentBar < barsBack + confirmBars) return false;
			
			double centerValue = GetValue(barsBack, isPrice, isHigh);
			
			// Check bars on both sides
			for (int i = 1; i <= confirmBars; i++)
			{
				double leftValue = GetValue(barsBack + i, isPrice, isHigh);
				double rightValue = GetValue(barsBack - i, isPrice, isHigh);
				
				if (isHigh)
				{
					if (leftValue >= centerValue || rightValue >= centerValue)
						return false;
				}
				else // isLow
				{
					if (leftValue <= centerValue || rightValue <= centerValue)
						return false;
				}
			}
			return true;
		}
		
		private double GetValue(int barsBack, bool isPrice, bool isHigh)
		{
			if (isPrice)
				return isHigh ? High[barsBack] : Low[barsBack];
			else
				return MacdValue[barsBack];
		}
		
		private void CheckDivergence(List<int> priceBars, List<int> macdBars, bool checkHighs, bool isRegular, bool isHidden)
		{
			if (priceBars.Count < 2 || macdBars.Count < 2) return;
			
			// Get most recent peaks
			int recentPrice = priceBars[0];
			int prevPrice = priceBars[1];
			
			// Find corresponding MACD peaks
			int recentMacd = FindClosestPeak(recentPrice, macdBars);
			int prevMacd = FindClosestPeak(prevPrice, macdBars);
			
			if (recentMacd == -1 || prevMacd == -1) return;
			
			// Get values
			double recentPriceVal = GetValue(recentPrice, true, checkHighs);
			double prevPriceVal = GetValue(prevPrice, true, checkHighs);
			double recentMacdVal = MacdValue[recentMacd];
			double prevMacdVal = MacdValue[prevMacd];
			
			// Check divergence conditions
			bool priceCondition, macdCondition;
			
			if (isRegular)
			{
				if (checkHighs) // Bearish regular
				{
					priceCondition = recentPriceVal > prevPriceVal;   // Higher high
					macdCondition = recentMacdVal < prevMacdVal;      // Lower high
				}
				else // Bullish regular
				{
					priceCondition = recentPriceVal < prevPriceVal;   // Lower low
					macdCondition = recentMacdVal > prevMacdVal;      // Higher low
				}
			}
			else // Hidden
			{
				if (checkHighs) // Hidden bullish (using highs)
				{
					priceCondition = recentPriceVal < prevPriceVal;   // Lower high
					macdCondition = recentMacdVal > prevMacdVal;      // Higher high
				}
				else // Hidden bearish (using lows)
				{
					priceCondition = recentPriceVal > prevPriceVal;   // Higher low
					macdCondition = recentMacdVal < prevMacdVal;      // Lower low
				}
			}
			
			// Verify significant movement
			double priceDiff = Math.Abs(recentPriceVal - prevPriceVal);
			double macdDiff = Math.Abs(recentMacdVal - prevMacdVal);
			
			if (priceCondition && macdCondition && priceDiff > 1.0 && macdDiff > 0.001)
			{
				// Determine color and tag
				Brush color;
				string prefix;
				
				if (isRegular)
				{
					color = checkHighs ? BearishDivergenceColor : BullishDivergenceColor;
					prefix = checkHighs ? "BearDiv" : "BullDiv";
				}
				else
				{
					color = checkHighs ? HiddenBullishDivColor : HiddenBearishDivColor;
					prefix = checkHighs ? "HidBullDiv" : "HidBearDiv";
				}
				
				string tag = prefix + "_" + CurrentBar + "_" + recentPrice;
				DrawDivergenceLine(prevMacd, recentMacd, prevMacdVal, recentMacdVal, color, tag);
			}
		}
		
		private int FindClosestPeak(int targetBar, List<int> peakBars)
		{
			foreach (int peakBar in peakBars)
			{
				if (Math.Abs(peakBar - targetBar) <= MinDivergenceStrength)
					return peakBar;
			}
			return -1;
		}
		
		private void DrawDivergenceLine(int barsBack1, int barsBack2, double macdVal1, double macdVal2, Brush color, string tag)
		{
			if (ChartControl == null) return;
			
			try
			{
				Draw.Line(this, tag, false, barsBack1, macdVal1, barsBack2, macdVal2, color, DashStyleHelper.Solid, 2);
			}
			catch
			{
				// Ignore drawing errors
			}
		}
		
		private void DrawDivergenceLine(int priceBar1, int priceBar2, int macdBar1, int macdBar2, Brush color, string tag)
		{
			if (ChartControl == null)
				return;
				
			// Draw line on MACD panel connecting the divergent MACD points
			Draw.Line(this, tag, false, 
				CurrentBar - macdBar1, MacdValue[CurrentBar - macdBar1],
				CurrentBar - macdBar2, MacdValue[CurrentBar - macdBar2],
				color, DashStyleHelper.Solid, 2);
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Fast period", Description="Number of bars for fast EMA", Order=1, GroupName="Parameters")]
		public int Fast
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Slow period", Description="Number of bars for slow EMA", Order=2, GroupName="Parameters")]
		public int Slow
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Smoothing", Description="Number of bars for smoothing.", Order=3, GroupName="Parameters")]
		public int Smooth
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Moving Average Type", Description="Select EMA or SMA for MACD calculation", Order=4, GroupName="Parameters")]
		public CustomEnumNamespaceDIV.UniversalMovingAverage MAType
		{
			get { return maType; }
			set { maType = value; }
		}

		
		[Display(Name="MACD Up/Down Color", Description="Show the MACD and Signal Line with directional color?", Order=1, GroupName="Display Options")]
		public bool ShowMACD
		{ get; set; }

		[Display(Name="Histogram Up/Down Color", Description="Show the MACD Histogram with directional color?", Order=2, GroupName="Display Options")]
		public bool ShowHistogram
		{ get; set; }

		[Display(Name="Background Up/Down Color", Description="Show the chart background with directional color?", Order=3, GroupName="Display Options")]
		public bool ShowBackColor
		{ get; set; }

		[Display(Name="Show Divergences", Description="Enable divergence detection and display", Order=1, GroupName="Divergence Settings")]
		public bool ShowDivergences
		{ get; set; }

		[Range(2, 20)]
		[Display(Name="Divergence Lookback", Description="Bars to look back for peak/trough detection", Order=2, GroupName="Divergence Settings")]
		public int DivergenceLookback
		{ get; set; }

		[Range(2, 50)]
		[Display(Name="Min Divergence Strength", Description="Minimum bars between divergence points", Order=3, GroupName="Divergence Settings")]
		public int MinDivergenceStrength
		{ get; set; }

		[Display(Name="Show Regular Divergences", Description="Show regular bullish/bearish divergences", Order=4, GroupName="Divergence Settings")]
		public bool ShowRegularDiv
		{ get; set; }

		[Display(Name="Show Hidden Divergences", Description="Show hidden bullish/bearish divergences", Order=5, GroupName="Divergence Settings")]
		public bool ShowHiddenDiv
		{ get; set; }

///Remove remark to restore PaintBar function.
//		[Display(Name="Price Bars Up/Down Color", Description="Color the price bars according to the MACD or the Histogram?", Order=4, GroupName="Options")]
//		public bool PaintBars
//		{ get; set; }

///Remove remark to restore PaintBar function.
//		[Display(Name="Paint Bars Type", Description="Paint Bars Input", Order=5, GroupName="Options")]
//		public Macd_PaintBarType2 pbType
//		{ get; set; }	

		
		[XmlIgnore]
		[Display(Name="Histogram rising above zero", Description="Macd / Hist > 0 (Rising)", Order=1, GroupName="Plot colors")]
		public Brush HistUpRising
		{ get; set; }

		[Browsable(false)]
		public string HistUpRisingSerializable
		{
			get { return Serialize.BrushToString(HistUpRising); }
			set { HistUpRising = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Histogram falling above zero", Description="Macd / Hist > 0 (Falling)", Order=2, GroupName="Plot colors")]
		public Brush HistUpFalling
		{ get; set; }

		[Browsable(false)]
		public string HistUpFallingSerializable
		{
			get { return Serialize.BrushToString(HistUpFalling); }
			set { HistUpFalling = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Histogram falling below zero", Description="Macd / Hist < 0 (Falling)", Order=3, GroupName="Plot colors")]
		public Brush HistDownFalling
		{ get; set; }

		[Browsable(false)]
		public string HistDownFallingSerializable
		{
			get { return Serialize.BrushToString(HistDownFalling); }
			set { HistDownFalling = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Histogram rising below zero", Description="Macd / Hist < 0 (Rising)", Order=4, GroupName="Plot colors")]
		public Brush HistDownRising
		{ get; set; }

		[Browsable(false)]
		public string HistDownRisingSerializable
		{
			get { return Serialize.BrushToString(HistDownRising); }
			set { HistDownRising = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Signal color rising", Description="Signal Line Rising", Order=5, GroupName="Plot colors")]
		public Brush SignalColorRising
		{ get; set; }

		[Browsable(false)]
		public string SignalColorRisingSerializable
		{
			get { return Serialize.BrushToString(SignalColorRising); }
			set { SignalColorRising = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="Signal color falling", Description="Signal Line Falling", Order=6, GroupName="Plot colors")]
		public Brush SignalColorFalling
		{ get; set; }

		[Browsable(false)]
		public string SignalColorFallingSerializable
		{
			get { return Serialize.BrushToString(SignalColorFalling); }
			set { SignalColorFalling = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="MACD rising color", Description="MACD Rising", Order=7, GroupName="Plot colors")]
		public Brush MACDRising
		{ get; set; }

		[Browsable(false)]
		public string MACDRisingSerializable
		{
			get { return Serialize.BrushToString(MACDRising); }
			set { MACDRising = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name="MACD falling color", Description="MACD Falling", Order=8, GroupName="Plot colors")]
		public Brush MACDFalling
		{ get; set; }

		[Browsable(false)]
		public string MACDFallingSerializable
		{
			get { return Serialize.BrushToString(MACDFalling); }
			set { MACDFalling = Serialize.StringToBrush(value); }
		}			

		// Divergence Color Properties
		[XmlIgnore]
		[Display(Name="Bullish Divergence", Description="Color for bullish divergence lines", Order=1, GroupName="Divergence Colors")]
		public Brush BullishDivergenceColor
		{ get; set; }

		[Browsable(false)]
		public string BullishDivergenceColorSerializable
		{
			get { return Serialize.BrushToString(BullishDivergenceColor); }
			set { BullishDivergenceColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name="Bearish Divergence", Description="Color for bearish divergence lines", Order=2, GroupName="Divergence Colors")]
		public Brush BearishDivergenceColor
		{ get; set; }

		[Browsable(false)]
		public string BearishDivergenceColorSerializable
		{
			get { return Serialize.BrushToString(BearishDivergenceColor); }
			set { BearishDivergenceColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name="Hidden Bullish Divergence", Description="Color for hidden bullish divergence lines", Order=3, GroupName="Divergence Colors")]
		public Brush HiddenBullishDivColor
		{ get; set; }

		[Browsable(false)]
		public string HiddenBullishDivColorSerializable
		{
			get { return Serialize.BrushToString(HiddenBullishDivColor); }
			set { HiddenBullishDivColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name="Hidden Bearish Divergence", Description="Color for hidden bearish divergence lines", Order=4, GroupName="Divergence Colors")]
		public Brush HiddenBearishDivColor
		{ get; set; }

		[Browsable(false)]
		public string HiddenBearishDivColorSerializable
		{
			get { return Serialize.BrushToString(HiddenBearishDivColor); }
			set { HiddenBearishDivColor = Serialize.StringToBrush(value); }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> MacdValue
		{
			get { return Values[2]; }
		}
		
		[Browsable(false)]
		[XmlIgnore]
		public Series<double> Avg
		{
			get { return Values[1]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> Hist
		{
			get { return Values[0]; }
		}



///Remove remark to restore MarketAnalyzer function.
//		[Browsable(false)]
//		[XmlIgnore]
//		public Series<double> MarketAnalyzer
//		{
//			get { return Values[3]; }
//		}

		#endregion

	}
}

// Custom namespace for enum - follows NinjaTrader best practices
namespace CustomEnumNamespaceDIV
{
	public enum UniversalMovingAverage
	{
		EMA,
		SMA
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Myindicators.MyMACDdiv[] cacheMyMACDdiv;
		public Myindicators.MyMACDdiv MyMACDdiv(int fast, int slow, int smooth, CustomEnumNamespaceDIV.UniversalMovingAverage mAType)
		{
			return MyMACDdiv(Input, fast, slow, smooth, mAType);
		}

		public Myindicators.MyMACDdiv MyMACDdiv(ISeries<double> input, int fast, int slow, int smooth, CustomEnumNamespaceDIV.UniversalMovingAverage mAType)
		{
			if (cacheMyMACDdiv != null)
				for (int idx = 0; idx < cacheMyMACDdiv.Length; idx++)
					if (cacheMyMACDdiv[idx] != null && cacheMyMACDdiv[idx].Fast == fast && cacheMyMACDdiv[idx].Slow == slow && cacheMyMACDdiv[idx].Smooth == smooth && cacheMyMACDdiv[idx].MAType == mAType && cacheMyMACDdiv[idx].EqualsInput(input))
						return cacheMyMACDdiv[idx];
			return CacheIndicator<Myindicators.MyMACDdiv>(new Myindicators.MyMACDdiv(){ Fast = fast, Slow = slow, Smooth = smooth, MAType = mAType }, input, ref cacheMyMACDdiv);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Myindicators.MyMACDdiv MyMACDdiv(int fast, int slow, int smooth, CustomEnumNamespaceDIV.UniversalMovingAverage mAType)
		{
			return indicator.MyMACDdiv(Input, fast, slow, smooth, mAType);
		}

		public Indicators.Myindicators.MyMACDdiv MyMACDdiv(ISeries<double> input , int fast, int slow, int smooth, CustomEnumNamespaceDIV.UniversalMovingAverage mAType)
		{
			return indicator.MyMACDdiv(input, fast, slow, smooth, mAType);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Myindicators.MyMACDdiv MyMACDdiv(int fast, int slow, int smooth, CustomEnumNamespaceDIV.UniversalMovingAverage mAType)
		{
			return indicator.MyMACDdiv(Input, fast, slow, smooth, mAType);
		}

		public Indicators.Myindicators.MyMACDdiv MyMACDdiv(ISeries<double> input , int fast, int slow, int smooth, CustomEnumNamespaceDIV.UniversalMovingAverage mAType)
		{
			return indicator.MyMACDdiv(input, fast, slow, smooth, mAType);
		}
	}
}

#endregion
