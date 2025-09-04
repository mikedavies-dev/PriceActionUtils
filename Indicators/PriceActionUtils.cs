// Copyright (C) 2025, mwwad4

#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Net.Http;
using System.IO;
using System.Web.Script.Serialization;
using SharpDX.DirectWrite;
using SharpDX;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class PriceActionUtils : Indicator
	{
		private bool isDrawing = false;
		private System.Windows.Point startPoint;
		private System.Windows.Point endPoint;
		private double startPrice;
		private double endPrice;
		private DateTime startTime;
		private DateTime endTime;
		private bool hasDrawnMeasure = false;

		private string version = "1.1.0";
		
		// Tick refresh functionality
		private long oldTimeFrame;
		private bool isRefreshing;
		
		// EMA and tick count functionality
		private Gui.Tools.SimpleFont textFont;
		private double askPrice, bidPrice;
		
		// Time bar tracking
		private bool marketOpenBarDrawn = false;
		private bool stopTradingBarDrawn = false;
		private DateTime currentDay = DateTime.MinValue;
		
		// NewsGuard functionality
		private volatile List<PriceActionUtilsNewsEvent> newsEvents = new List<PriceActionUtilsNewsEvent>();
		private long lastFetchTimeTicks = DateTime.MinValue.Ticks;
		private string tempFilePath;
		private TextFormat newsTextFormat;
		private SimpleFont newsFont;
		private volatile bool isFetchingNews = false;

		[Display(Name="Version", Description="Indicator Version", Order=1, GroupName="About")]
		public string IndicatorVersion
		{
			get { return version; }
			set {  }
        }

		[Display(Name="Enable Quick Measure", Description="Enable/disable the quick measure tool", Order=1, GroupName="Quick Measure")]
		public bool EnableQuickMeasure
		{ get; set; }

		[Display(Name="Require Control Key", Description="Require Control key to be pressed to activate quick measure", Order=2, GroupName="Quick Measure")]
		public bool RequireControlKey
		{ get; set; }
		
		[XmlIgnore]
		[Display(Name="Upward Fill Color", Order=3, GroupName="Quick Measure")]
		public Brush UpwardFillColor
		{ get; set; }
		
		[Browsable(false)]
		public string UpwardFillColorSerializable
		{
			get { return Serialize.BrushToString(UpwardFillColor); }
			set { UpwardFillColor = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(Name="Downward Fill Color", Order=4, GroupName="Quick Measure")]
		public Brush DownwardFillColor
		{ get; set; }
		
		[Browsable(false)]
		public string DownwardFillColorSerializable
		{
			get { return Serialize.BrushToString(DownwardFillColor); }
			set { DownwardFillColor = Serialize.StringToBrush(value); }
		}
		
		[Range(0, 100)]
		[Display(Name="Fill Opacity %", Order=3, GroupName="Quick Measure")]
		public int FillOpacity
		{ get; set; }
		
		[Range(8, 36)]
		[Display(Name="Text Size", Order=5, GroupName="Quick Measure")]
		public int TextSize
		{ get; set; }
		
		[Range(0, int.MaxValue)]
		[Display(Name="Refresh Time Interval (ms)", Description="Milliseconds between chart refreshes. 0 = refresh on every tick, 10 = max 100 fps, 20 = max 50 fps", Order=1, GroupName="Max Chart Refresh Rate")]
		public int RefreshTimeInterval
		{ get; set; }
		
		[Display(Name="Show EMA", Description="Display EMA 21 on chart", Order=1, GroupName="EMA")]
		public bool ShowEMA
		{ get; set; }
		
		[Display(Name="Show Tick Count", Description="Display remaining tick count", Order=1, GroupName="Ticks")]
		public bool ShowTickCount
		{ get; set; }
		
		[Display(Name="Count Down", Description="Show remaining instead of elapsed ticks", Order=2, GroupName="Ticks")]
		public bool CountDown
		{ get; set; }
		
		[Display(Name="Show Percent", Description="Show percentage instead of tick count", Order=3, GroupName="Ticks")]
		public bool ShowPercent
		{ get; set; }

		[Display(Name="Show Previous Bar Range", Description="Display previous bar size in points", Order=4, GroupName="Ticks")]
		public bool ShowPreviousBarRange
		{ get; set; }
		
		[Display(Name="Price Line On", Description="Display horizontal price line at current close price", Order=1, GroupName="Price Lines")]
		public bool PricelineOn
		{ get; set; }
		
		[Display(Name="Spread Lines On", Description="Display ask/bid spread lines", Order=2, GroupName="Price Lines")]
		public bool SpreadlinesOn
		{ get; set; }


		[Display(Name="Show Market Open Bar", Description="Display vertical bar at market open (08:30)", Order=1, GroupName="Time Bars")]
		public bool ShowMarketOpenBar
		{ get; set; }

		[Display(Name="Show Stop Trading Bar", Description="Display vertical bar at stop trading time (14:30)", Order=2, GroupName="Time Bars")]
		public bool ShowStopTradingBar
		{ get; set; }

		[XmlIgnore]
		[Display(Name="Market Open Bar Color", Order=3, GroupName="Time Bars")]
		public Brush MarketOpenBarColor
		{ get; set; }

		[Browsable(false)]
		public string MarketOpenBarColorSerializable
		{
			get { return Serialize.BrushToString(MarketOpenBarColor); }
			set { MarketOpenBarColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name="Stop Trading Bar Color", Order=16, GroupName="Time Bars")]
		public Brush StopTradingBarColor
		{ get; set; }

		[Browsable(false)]
		public string StopTradingBarColorSerializable
		{
			get { return Serialize.BrushToString(StopTradingBarColor); }
			set { StopTradingBarColor = Serialize.StringToBrush(value); }
		}

		[Range(1, 10)]
		[Display(Name="Time Bar Width", Order=4, GroupName="Time Bars")]
		public int TimeBarWidth
		{ get; set; }

		[Range(0, 100)]
		[Display(Name="Market Open Bar Opacity %", Order=5, GroupName="Time Bars")]
		public int MarketOpenBarOpacity
		{ get; set; }

		[Range(0, 100)]
		[Display(Name="Stop Trading Bar Opacity %", Order=6, GroupName="Time Bars")]
		public int StopTradingBarOpacity
		{ get; set; }

		[Display(Name="Market Open Bar Style", Order=7, GroupName="Time Bars")]
		public DashStyleHelper MarketOpenBarStyle
		{ get; set; }

		[Display(Name="Stop Trading Bar Style", Order=8, GroupName="Time Bars")]
		public DashStyleHelper StopTradingBarStyle
		{ get; set; }

		[Display(Name="Market Open Time", Description="Time for market open bar (HH:MM format)", Order=9, GroupName="Time Bars")]
		public string MarketOpenTime
		{ get; set; }

		[Display(Name="Last Trade Time", Description="Time for last trade bar (HH:MM format)", Order=10, GroupName="Time Bars")]
		public string LastTradeTime
		{ get; set; }

		// NewsGuard Properties
		[Display(Name="Enable NewsGuard", Description="Enable/disable the NewsGuard display", Order=1, GroupName="News Guard")]
		public bool EnableNewsGuard
		{ get; set; }

		[Range(1, 24)]
		[Display(Name="Refresh Interval (Hours)", Description="How often to fetch new data", Order=2, GroupName="News Guard")]
		public int NewsRefreshIntervalHours
		{ get; set; }

		[Display(Name="Countries", Description="Comma-separated list of currency codes (e.g., USD,EUR,GBP)", Order=3, GroupName="News Guard")]
		public string NewsCountries
		{ get; set; }

		[Range(1, 60)]
		[Display(Name="Recent Event Minutes", Description="Minutes to consider event as recent (for NOW display, urgent color, and green arrow)", Order=4, GroupName="News Guard")]
		public int NewsRecentEventMinutes
		{ get; set; }

		[Display(Name="Show Low Impact", Order=5, GroupName="News Guard")]
		public bool NewsShowLowImpact
		{ get; set; }

		[Display(Name="Show Medium Impact", Order=6, GroupName="News Guard")]
		public bool NewsShowMediumImpact
		{ get; set; }

		[Display(Name="Show High Impact", Order=7, GroupName="News Guard")]
		public bool NewsShowHighImpact
		{ get; set; }

		[Display(Name="Show Column Header", Order=8, GroupName="News Guard")]
		public bool NewsShowColumnHeader
		{ get; set; }

		[XmlIgnore]
		[Display(Name="Text Color", Order=9, GroupName="News Guard")]
		public Brush NewsTextColor
		{ get; set; }

		[Browsable(false)]
		public string NewsTextColorSerializable
		{
			get { return Serialize.BrushToString(NewsTextColor); }
			set { NewsTextColor = Serialize.StringToBrush(value); }
		}

		[Display(Name="Font", Order=10, GroupName="News Guard")]
		public SimpleFont NewsDisplayFont
		{ get; set; }

		[XmlIgnore]
		[Display(Name="High Impact Folder", Order=11, GroupName="News Guard")]
		public Brush NewsFolderHighColor
		{ get; set; }

		[Browsable(false)]
		public string NewsFolderHighColorSerializable
		{
			get { return Serialize.BrushToString(NewsFolderHighColor); }
			set { NewsFolderHighColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name="Medium Impact Folder", Order=12, GroupName="News Guard")]
		public Brush NewsFolderMediumColor
		{ get; set; }

		[Browsable(false)]
		public string NewsFolderMediumColorSerializable
		{
			get { return Serialize.BrushToString(NewsFolderMediumColor); }
			set { NewsFolderMediumColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name="Low Impact Folder", Order=13, GroupName="News Guard")]
		public Brush NewsFolderLowColor
		{ get; set; }

		[Browsable(false)]
		public string NewsFolderLowColorSerializable
		{
			get { return Serialize.BrushToString(NewsFolderLowColor); }
			set { NewsFolderLowColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name="Urgent Countdown Color", Order=14, GroupName="News Guard")]
		public Brush NewsUrgentCountdownColor
		{ get; set; }

		[Browsable(false)]
		public string NewsUrgentCountdownColorSerializable
		{
			get { return Serialize.BrushToString(NewsUrgentCountdownColor); }
			set { NewsUrgentCountdownColor = Serialize.StringToBrush(value); }
		}


		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Add some price action extras to your chart, such as quick mesure and increased refresh rate";
				Name = "PriceActionUtils";
				Calculate = Calculate.OnEachTick;
				IsOverlay = true;
				DisplayInDataBox = true;
				DrawOnPricePanel = true;
				DrawHorizontalGridLines = true;
				DrawVerticalGridLines = true;
				PaintPriceMarkers = true;
				ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive = true;
				
				// Set default colors
				UpwardFillColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 255, 0)); // 50% Green
				DownwardFillColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 250, 128, 114)); // 50% Salmon
				FillOpacity = 50;
				TextSize = 16;
				RefreshTimeInterval = 10;
				textFont = new Gui.Tools.SimpleFont("Arial", 18);
				ShowEMA = true;
				ShowTickCount = true;
				CountDown = true;
				ShowPercent = false;
				PricelineOn = false;
				SpreadlinesOn = true;
				ShowPreviousBarRange = false;
				ShowMarketOpenBar = true;
				ShowStopTradingBar = true;
				MarketOpenBarColor = Brushes.Green;
				StopTradingBarColor = Brushes.Red;
				TimeBarWidth = 3;
				MarketOpenBarOpacity = 50;
				StopTradingBarOpacity = 50;
				MarketOpenBarStyle = DashStyleHelper.Dash;
				StopTradingBarStyle = DashStyleHelper.Dash;
				MarketOpenTime = "08:30";
				LastTradeTime = "14:30";
				EnableQuickMeasure = true;
				RequireControlKey = false;
				
				// NewsGuard defaults
				EnableNewsGuard = false;
				NewsRefreshIntervalHours = 1;
				NewsCountries = "USD";
				NewsShowLowImpact = false;
				NewsShowMediumImpact = true;
				NewsShowHighImpact = true;
				NewsTextColor = Brushes.Black;
				NewsDisplayFont = new SimpleFont("Arial", 13);
				NewsFolderHighColor = Brushes.Red;
				NewsFolderMediumColor = Brushes.Orange;
				NewsFolderLowColor = Brushes.Gold;
				NewsUrgentCountdownColor = Brushes.Red;
				NewsRecentEventMinutes = 5;
				NewsShowColumnHeader = false;
				
				AddPlot(new Stroke(Brushes.DarkViolet, DashStyleHelper.Solid, 3), PlotStyle.Line, "EMA21");
			}
			else if (State == State.Configure)
			{
				tempFilePath = Path.Combine(Path.GetTempPath(), "PriceActionUtils_NewsGuard");
			}
			else if (State == State.DataLoaded)
			{
				if (EnableNewsGuard)
				{
					LoadNewsData();
				}
			}
			else if (State == State.Historical)
			{
				if (ChartControl != null)
				{
					ChartControl.MouseRightButtonDown += OnMouseRightButtonDown;
					ChartControl.MouseMove += OnMouseMove;
					ChartControl.MouseRightButtonUp += OnMouseRightButtonUp;
				}
			}
			else if (State == State.Terminated)
			{
				if (ChartControl != null)
				{
					ChartControl.MouseRightButtonDown -= OnMouseRightButtonDown;
					ChartControl.MouseMove -= OnMouseMove;
					ChartControl.MouseRightButtonUp -= OnMouseRightButtonUp;
				}
				
				if (newsTextFormat != null && !newsTextFormat.IsDisposed)
					newsTextFormat.Dispose();
			}
		}

		protected override void OnBarUpdate()
		{
			// EMA calculation - always calculate for plot
			EMA21[0] = EMA(Close, 21)[0];
			
			// Get current bid/ask prices
			askPrice = GetCurrentAsk();
			bidPrice = GetCurrentBid();
			
			// Control EMA plot visibility
			if (!ShowEMA)
			{
				Plots[0].Brush = Brushes.Transparent;
			}
			
			// NewsGuard data refresh check
			if (EnableNewsGuard && !isFetchingNews)
			{
				var lastFetch = new DateTime(Interlocked.Read(ref lastFetchTimeTicks));
				if (DateTime.Now.Subtract(lastFetch).TotalHours >= NewsRefreshIntervalHours)
				{
					StartNewsDataFetch();
				}
			}
			
			// Tick count and previous bar range display
			if (ShowTickCount || ShowPreviousBarRange)
			{
				string displayText = "";
				
				// Add tick count if enabled
				if (ShowTickCount)
				{
					double periodValue = BarsPeriod.Value;

                    double tickCount = ShowPercent
						? CountDown ? (1 - Bars.PercentComplete) * 100
						: Bars.PercentComplete * 100 : CountDown
							? periodValue - Bars.TickCount
							: Bars.TickCount;

					displayText = tickCount + (ShowPercent ? "%" : "");
				}
				
				// Add previous bar range if enabled
				if (ShowPreviousBarRange && CurrentBar > 0)
				{
					double prevBarRange = (High[1] - Low[1]);
					string rangeText = $"{prevBarRange:F2}";
					
					if (!string.IsNullOrEmpty(displayText))
						displayText += "  " + rangeText;
					else
						displayText = rangeText;
				}
				
				if (!string.IsNullOrEmpty(displayText))
				{
					if (PricelineOn)
					{
						Draw.Text(this, "TickCount", false, displayText, -1, Close[0] + 3 * TickSize, 0, ChartControl.Properties.ChartText, textFont, System.Windows.TextAlignment.Left, Brushes.Transparent, Brushes.Black, 0);
					}
					else
					{
						Draw.Text(this, "TickCount", false, displayText, -3, Close[0], 0, ChartControl.Properties.ChartText, textFont, System.Windows.TextAlignment.Left, Brushes.Transparent, Brushes.Black, 0);
					}
				}
			}
			
			// Draw price lines if enabled
			if (PricelineOn)
			{
				Draw.HorizontalLine(this, "PriceLine", false, Close[0], Brushes.Black, DashStyleHelper.Dot, 1);
			}
			
			// Draw spread lines if enabled
			if (SpreadlinesOn)
			{
				int lineLength = PricelineOn ? -5 : -3;
				Draw.Line(this, "AskLine", false, 0, askPrice, lineLength, askPrice, Brushes.Red, DashStyleHelper.Solid, 2);
				Draw.Line(this, "BidLine", false, 0, bidPrice, lineLength, bidPrice, Brushes.Blue, DashStyleHelper.Solid, 2);
			}
			
			// Draw time bars if enabled
			DateTime barTime = Time[0];
			DateTime barDate = barTime.Date;
			
			// Reset flags for new day
			if (barDate != currentDay)
			{
				currentDay = barDate;
				marketOpenBarDrawn = false;
				stopTradingBarDrawn = false;
			}
			
			// Draw market open bar
			TimeSpan marketOpenTimeSpan = ParseTimeString(MarketOpenTime);
			if (ShowMarketOpenBar && !marketOpenBarDrawn && barTime.TimeOfDay >= marketOpenTimeSpan)
			{
				Brush marketOpenBrush = ApplyOpacity(MarketOpenBarColor, MarketOpenBarOpacity);
				var marketOpenLine = Draw.VerticalLine(this, "MarketOpen_" + barTime.ToString("yyyyMMdd"), 0, marketOpenBrush, MarketOpenBarStyle, TimeBarWidth);
				marketOpenLine.ZOrder = -1; // Draw behind main series
				marketOpenBarDrawn = true;
			}
			
			// Draw stop trading bar  
			TimeSpan lastTradeTimeSpan = ParseTimeString(LastTradeTime);
			if (ShowStopTradingBar && !stopTradingBarDrawn && barTime.TimeOfDay >= lastTradeTimeSpan)
			{
				Brush stopTradingBrush = ApplyOpacity(StopTradingBarColor, StopTradingBarOpacity);
				var stopTradingLine = Draw.VerticalLine(this, "StopTrading_" + barTime.ToString("yyyyMMdd"), 0, stopTradingBrush, StopTradingBarStyle, TimeBarWidth);
				stopTradingLine.ZOrder = -1; // Draw behind main series
				stopTradingBarDrawn = true;
			}
			
			// Tick refresh functionality
			if (isRefreshing || ChartControl == null || State == State.Historical) {
				return;
			}
			
			if (RefreshTimeInterval > 0) {
				long newTimeFrame = (long)(DateTime.Now.Ticks / (10000*RefreshTimeInterval));
				if (newTimeFrame == oldTimeFrame) {
					return;
				}
				oldTimeFrame = newTimeFrame;
			}
			isRefreshing = true;
			ChartControl.Dispatcher.InvokeAsync(() => {
				ChartControl.InvalidateVisual();
				isRefreshing = false;
			});
		}

		private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (ChartControl == null || ChartPanel == null || !EnableQuickMeasure)
				return;

			// Check if Control key is required and pressed
			if (RequireControlKey && !Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
				return;

			startPoint = e.GetPosition(ChartControl);
			var startPointPanel = e.GetPosition(ChartPanel);
			
			var chartScale = ChartPanel.Scales.FirstOrDefault(s => s.IsVisible);
			if (chartScale == null)
				return;

			// Apply DPI scaling to Y coordinate for price calculation
			var dpiScale = ChartControl.PresentationSource?.CompositionTarget?.TransformToDevice ?? new System.Windows.Media.Matrix(1, 0, 0, 1, 0, 0);
			float scaleY = (float)dpiScale.M22;

			float scaledY = (float)(startPointPanel.Y * scaleY);
		
			double priceMethod1 = chartScale.GetValueByY((float)startPointPanel.Y);
			double priceMethod2 = chartScale.GetValueByY(scaledY);
		
			startPrice = priceMethod2; // Use DPI-scaled method
			int barIndex = (int)ChartControl.GetSlotIndexByX((int)(float)startPoint.X);

            startTime = Bars.GetTime(barIndex);
            hasDrawnMeasure = false;
            isDrawing = true;
            e.Handled = true;
		}
		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			if (!isDrawing || ChartControl == null || ChartPanel == null || !EnableQuickMeasure)
				return;

			hasDrawnMeasure = true;

			endPoint = e.GetPosition(ChartControl);
			var endPointPanel = e.GetPosition(ChartPanel);
			var chartScale = ChartPanel.Scales.FirstOrDefault(s => s.IsVisible);

			if (chartScale == null)
				return;
			
			// Apply DPI scaling to Y coordinate for price calculation
			var dpiScale = ChartControl.PresentationSource?.CompositionTarget?.TransformToDevice ?? new System.Windows.Media.Matrix(1, 0, 0, 1, 0, 0);
			float scaleY = (float)dpiScale.M22;
			float scaledY = (float)(endPointPanel.Y * scaleY);
		
			endPrice = chartScale.GetValueByY(scaledY);
			int barIndex = (int)ChartControl.GetSlotIndexByX((int)(float)endPoint.X);

			if (barIndex >= 0 && barIndex < Bars.Count)
			{
				endTime = Bars.GetTime(barIndex);
				ChartControl.InvalidateVisual();
			}
		}

		private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (isDrawing)
			{
				isDrawing = false;
				ChartControl.InvalidateVisual();

				// If we have drawn our tool then don't show bubble the event, this will prevent the default menu from showing
				if (hasDrawnMeasure)
					e.Handled = true;
			}
		}
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			// NewsGuard rendering
			if (EnableNewsGuard)
			{
				DrawNewsGuardDisplay(chartControl, chartScale);
			}

			// only draw if we are drawing and we have moved at least once to calculate the points
			if (!isDrawing || !hasDrawnMeasure || !EnableQuickMeasure)
				return;

			// Apply DPI scaling
			var dpiScale = ChartControl.PresentationSource?.CompositionTarget?.TransformToDevice ?? new System.Windows.Media.Matrix(1, 0, 0, 1, 0, 0);
			float scaleX = (float)dpiScale.M11;
			float scaleY = (float)dpiScale.M22;

			// Determine if price is moving up or down
			bool isPriceUp = endPrice > startPrice;
			Brush selectedBrush = isPriceUp ? UpwardFillColor : DownwardFillColor;
		
			using (var borderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Blue))
			using (var fillBrush = selectedBrush.ToDxBrush(RenderTarget, (float)FillOpacity / 100.0f))
			{
				var rect = new SharpDX.RectangleF(
					(float)(Math.Min(startPoint.X, endPoint.X) * scaleX),
					(float)(Math.Min(startPoint.Y, endPoint.Y) * scaleY),
					(float)(Math.Abs(endPoint.X - startPoint.X) * scaleX),
					(float)(Math.Abs(endPoint.Y - startPoint.Y) * scaleY)
				);
				
 				// Fill rectangle with selected color
				RenderTarget.FillRectangle(rect, fillBrush);
				
				string measurementInfo = GetMeasurementInfo();
				if (!string.IsNullOrEmpty(measurementInfo))
				{
					using (var textBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Black))
					using (var whiteBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.White))
					using (var blackBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Black))
					using (var textFormat = new SharpDX.DirectWrite.TextFormat(NinjaTrader.Core.Globals.DirectWriteFactory, "Arial", TextSize))
					{
						// Calculate text dimensions
						var textLayout = new SharpDX.DirectWrite.TextLayout(NinjaTrader.Core.Globals.DirectWriteFactory, measurementInfo, textFormat, 400, 200);
						float textWidth = textLayout.Metrics.Width;
						float textHeight = textLayout.Metrics.Height;
						
						// Add padding around text
						float padding = 8;
						float boxWidth = textWidth + (padding * 2);
						float boxHeight = textHeight + (padding * 2);
						
						// Position text box below and centered on main rectangle
						float rectCenterX = (float)((Math.Min(startPoint.X, endPoint.X) + Math.Max(startPoint.X, endPoint.X)) / 2 * scaleX);
						float rectBottomY = (float)(Math.Max(startPoint.Y, endPoint.Y) * scaleY);
						
						float textBoxX = rectCenterX - (boxWidth / 2);
						float textBoxY = rectBottomY + 10; // 10px gap below main rectangle
						
						var textBoxRect = new SharpDX.RectangleF(textBoxX, textBoxY, boxWidth, boxHeight);
						
						// Draw black border (1px)
						RenderTarget.DrawRectangle(textBoxRect, blackBrush, 3.0f);
						
						// Draw white background
						RenderTarget.FillRectangle(textBoxRect, whiteBrush);
						
						// Draw text centered in the box
						var textRect = new SharpDX.RectangleF(
							textBoxX + padding,
							textBoxY + padding,
							textWidth,
							textHeight
						);
						RenderTarget.DrawText(measurementInfo, textFormat, textRect, textBrush);
						
						textLayout.Dispose();
					}
				}
			}
		}
		private string GetMeasurementInfo()
		{
			if (!isDrawing)
				return "";

			// Apply DPI scaling for accurate bar calculations
			var dpiScale = ChartControl.PresentationSource?.CompositionTarget?.TransformToDevice ?? new System.Windows.Media.Matrix(1, 0, 0, 1, 0, 0);
			float scaleX = (float)dpiScale.M11;

			int startBarIndex = (int)ChartControl.GetSlotIndexByX((int)(startPoint.X * scaleX));
			int endBarIndex = (int)ChartControl.GetSlotIndexByX((int)(endPoint.X * scaleX));
			int barCount = Math.Abs(endBarIndex - startBarIndex);

			double priceDiff = Math.Abs(startPrice - endPrice);
			double tickSize = Bars.Instrument.MasterInstrument.TickSize;
			int ticks = (int)Math.Round(priceDiff / tickSize);
			double ticksPerPoint = 1.0 / tickSize;
			double points = ticks / ticksPerPoint;
			double tickValue = Bars.Instrument.MasterInstrument.PointValue * Bars.Instrument.MasterInstrument.TickSize;
			double totalValue = ticks * tickValue;

			// Get actual bar times for accurate time calculation
			DateTime actualStartTime = startBarIndex >= 0 && startBarIndex < Bars.Count ? Bars.GetTime(startBarIndex) : startTime;
			DateTime actualEndTime = endBarIndex >= 0 && endBarIndex < Bars.Count ? Bars.GetTime(endBarIndex) : endTime;
			
			TimeSpan timeDiff = actualEndTime - actualStartTime;

            if (timeDiff < TimeSpan.Zero)
                timeDiff = actualStartTime - actualEndTime;
            
            string timeString = $"{timeDiff.Hours:D2}:{timeDiff.Minutes:D2}:{timeDiff.Seconds:D2}";
            
            // Calculate bars per minute
            double barsPerMinute = 0;
            if (timeDiff.TotalMinutes > 0 && barCount > 0)
            {
                barsPerMinute = barCount / timeDiff.TotalMinutes;
            }
            
            string barsPerMinuteText = barsPerMinute > 0 ? $" ({barsPerMinute:F1}/min)" : "";
            
            return $"Bars: {barCount}{barsPerMinuteText} ({timeString})\nPoints: {points:F2} ({ticks} ticks)\nValue: ${totalValue:F2}";
		}
		
		private TimeSpan ParseTimeString(string timeStr)
		{
			try
			{
				if (string.IsNullOrEmpty(timeStr))
					return new TimeSpan(8, 30, 0);
					
				string[] parts = timeStr.Split(':');
				if (parts.Length == 2)
				{
					int hours = int.Parse(parts[0]);
					int minutes = int.Parse(parts[1]);
					return new TimeSpan(hours, minutes, 0);
				}
			}
			catch
			{
				// Return default time if parsing fails
			}
			return new TimeSpan(8, 30, 0);
		}
		
		private Brush ApplyOpacity(Brush originalBrush, int opacityPercent)
		{
			if (originalBrush is SolidColorBrush solidBrush)
			{
				var color = solidBrush.Color;
				byte alpha = (byte)(255 * opacityPercent / 100);
				var newColor = System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B);
				return new SolidColorBrush(newColor);
			}
			return originalBrush;
		}
		
		#region Logging Methods
		private void LogMessage(string message)
		{
			Print($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
		}
		#endregion
		
		#region NewsGuard Methods
		private void DrawNewsGuardDisplay(ChartControl chartControl, ChartScale chartScale)
		{
			// Update text format if needed
			if (newsTextFormat == null || newsTextFormat.IsDisposed || newsFont == null || !newsFont.Equals(NewsDisplayFont))
			{
				if (newsTextFormat != null && !newsTextFormat.IsDisposed)
					newsTextFormat.Dispose();
				newsFont = NewsDisplayFont;
				newsTextFormat = newsFont.ToDirectWriteTextFormat();
			}

			// Filter news events for today
			var filteredEvents = FilterNewsEventsForToday(chartControl);

			if (filteredEvents.Count == 0)
			{
				DrawNewsText("No news events for today.", ChartPanel.X + 10, ChartPanel.Y + 10, NewsTextColor);
				return;
			}

			// Draw news events
			DrawNewsEvents(chartControl, filteredEvents);
		}
		
		private List<PriceActionUtilsNewsEvent> FilterNewsEventsForToday(ChartControl chartControl)
		{
			// Get a snapshot of current events - volatile read ensures we get the latest
			var currentEvents = newsEvents;
			
			if (currentEvents == null || currentEvents.Count == 0)
				return new List<PriceActionUtilsNewsEvent>();

			var countryList = NewsCountries.Split(',').Select(c => c.Trim().ToUpper()).ToList();

			// Use local timezone for chart
			DateTime chartNow = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local);

			return currentEvents.Where(ne =>
				countryList.Contains(ne.Country.ToUpper()) &&
				IsSameDay(TimeZoneInfo.ConvertTime(ne.Date, TimeZoneInfo.Local), chartNow) &&
				((ne.Impact == "Low" && NewsShowLowImpact) ||
				 (ne.Impact == "Medium" && NewsShowMediumImpact) ||
				 (ne.Impact == "High" && NewsShowHighImpact))
			).OrderBy(ne => ne.Date).ToList();
		}

		private bool IsSameDay(DateTime date1, DateTime date2)
		{
			return date1.Date == date2.Date;
		}
		
		private void DrawNewsEvents(ChartControl chartControl, List<PriceActionUtilsNewsEvent> events)
		{
			float yPosition = ChartPanel.Y + 10;
			float lineHeight = (float)NewsDisplayFont.Size + 5;
			float rowSpacing = 2f; // Moderate spacing between rows

			// Draw header if enabled
			if (NewsShowColumnHeader)
			{
				DrawNewsBoldText("time", ChartPanel.X + 55, yPosition, NewsTextColor);
				DrawNewsBoldText("in", ChartPanel.X + 115, yPosition, NewsTextColor);
				DrawNewsBoldText("cur", ChartPanel.X + 195, yPosition, NewsTextColor);
				DrawNewsBoldText("news", ChartPanel.X + 255, yPosition, NewsTextColor);

				yPosition += lineHeight + 5;
			}

			// Find the next upcoming event
			var nextEvent = GetNextUpcomingEvent(events);

			// Draw events
			foreach (var newsEvent in events.Take(10)) // Limit to 10 events
			{
				var chartTime = TimeZoneInfo.ConvertTime(newsEvent.Date, TimeZoneInfo.Local);
				var timeText = chartTime.ToString("HH:mm");
				var countdownText = GetCountdownText(newsEvent.Date);
				var countdownColor = GetCountdownColor(newsEvent.Date);
				var folderColor = GetNewsFolderColor(newsEvent.Impact);
				
				// Check if this is the next upcoming event
				var isNextEvent = nextEvent != null && newsEvent == nextEvent;
				
				// Draw green arrow for next upcoming event (on the far left)
				if (isNextEvent)
				{
					DrawNextEventArrow(ChartPanel.X + 10, yPosition);
				}

				// Draw folder icon instead of text with background
				DrawNewsFolderIcon(ChartPanel.X + 25, yPosition, folderColor);
				
				DrawNewsText(timeText, ChartPanel.X + 55, yPosition, NewsTextColor);
				DrawNewsText(countdownText, ChartPanel.X + 115, yPosition, countdownColor);
				DrawNewsText(newsEvent.Country, ChartPanel.X + 195, yPosition, NewsTextColor);
				DrawNewsText(TruncateText(newsEvent.Title, 40), ChartPanel.X + 255, yPosition, NewsTextColor);

				yPosition += lineHeight + rowSpacing;

				if (yPosition > ChartPanel.Y + 280) // Stop if we're getting too close to the bottom
					break;
			}
		}
		
		private void DrawNewsText(string text, float x, float y, Brush color)
		{
			using (var textLayout = new TextLayout(Core.Globals.DirectWriteFactory, text, newsTextFormat, ChartPanel.W, newsTextFormat.FontSize))
			{
				RenderTarget.DrawTextLayout(new Vector2(x, y), textLayout, color.ToDxBrush(RenderTarget));
			}
		}
		
		private void DrawNewsBoldText(string text, float x, float y, Brush color)
		{
			// Create a bold font for headers
			var boldFont = new SimpleFont(NewsDisplayFont.Family.Source, NewsDisplayFont.Size) { Bold = true };
			using (var boldTextFormat = boldFont.ToDirectWriteTextFormat())
			using (var textLayout = new TextLayout(Core.Globals.DirectWriteFactory, text, boldTextFormat, ChartPanel.W, boldTextFormat.FontSize))
			{
				RenderTarget.DrawTextLayout(new Vector2(x, y), textLayout, color.ToDxBrush(RenderTarget));
			}
		}
		
		private void DrawNextEventArrow(float x, float y)
		{
			// Draw a green triangular arrow pointing right
			var arrowSize = 8f;
			var centerY = y + (float)NewsDisplayFont.Size / 2;
			
			// Create triangle points for right-pointing arrow
			var points = new Vector2[]
			{
				new Vector2(x, centerY - arrowSize / 2),      // Top left
				new Vector2(x, centerY + arrowSize / 2),      // Bottom left  
				new Vector2(x + arrowSize, centerY)           // Right point
			};
			
			using (var pathGeometry = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory))
			using (var geometrySink = pathGeometry.Open())
			{
				geometrySink.BeginFigure(points[0], SharpDX.Direct2D1.FigureBegin.Filled);
				geometrySink.AddLine(points[1]);
				geometrySink.AddLine(points[2]);
				geometrySink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
				geometrySink.Close();
				
				RenderTarget.FillGeometry(pathGeometry, Brushes.LimeGreen.ToDxBrush(RenderTarget));
			}
		}
		
		private void DrawNewsFolderIcon(float x, float y, Brush folderColor)
		{
			var centerY = y + (float)NewsDisplayFont.Size / 2;
			var folderWidth = 12f;
			var folderHeight = 9f;
			var tabWidth = 4f;
			var tabHeight = 2f;
			
			// Main folder body rectangle
			var folderRect = new SharpDX.RectangleF(
				x,
				centerY - folderHeight / 2,
				folderWidth,
				folderHeight
			);
			
			// Folder tab rectangle (small rectangle on top left)
			var tabRect = new SharpDX.RectangleF(
				x,
				centerY - folderHeight / 2 - tabHeight,
				tabWidth,
				tabHeight
			);
			
			// Draw the folder tab
			RenderTarget.FillRectangle(tabRect, folderColor.ToDxBrush(RenderTarget));
			
			// Draw the main folder body
			RenderTarget.FillRectangle(folderRect, folderColor.ToDxBrush(RenderTarget));
			
			// Add a subtle border for better definition
			var borderBrush = Brushes.Black.ToDxBrush(RenderTarget);
			borderBrush.Opacity = 0.3f;
			RenderTarget.DrawRectangle(folderRect, borderBrush, 0.5f);
		}
		
		private Brush GetNewsFolderColor(string impact)
		{
			switch (impact)
			{
				case "High": return NewsFolderHighColor;
				case "Medium": return NewsFolderMediumColor;
				case "Low": return NewsFolderLowColor;
				default: return Brushes.Transparent;
			}
		}
		
		private string GetCountdownText(DateTime eventTime)
		{
			var now = DateTime.Now;
			var localEventTime = TimeZoneInfo.ConvertTime(eventTime, TimeZoneInfo.Local);
			
			if (localEventTime <= now)
			{
				// Show time ago that the event occurred
				var timeSinceEvent = now - localEventTime;
				if (timeSinceEvent.TotalMinutes <= NewsRecentEventMinutes)
				{
					// Show time ago for recent events within the threshold
					if (timeSinceEvent.TotalMinutes < 1)
					{
						return $"{(int)timeSinceEvent.TotalSeconds}s ago";
					}
					else if (timeSinceEvent.TotalHours < 1)
					{
						return $"{(int)timeSinceEvent.TotalMinutes}m ago";
					}
					else
					{
						return $"{(int)timeSinceEvent.TotalHours}h {timeSinceEvent.Minutes}m ago";
					}
				}
				return "-"; // Event has passed more than threshold ago
			}
			
			var timeRemaining = localEventTime - now;
			
			if (timeRemaining.TotalDays >= 1)
			{
				return $"{(int)timeRemaining.TotalDays}d {timeRemaining.Hours}h";
			}
			else if (timeRemaining.TotalHours >= 1)
			{
				return $"{timeRemaining.Hours}h {timeRemaining.Minutes}m";
			}
			else if (timeRemaining.TotalMinutes >= 1)
			{
				return $"{timeRemaining.Minutes}m {timeRemaining.Seconds}s";
			}
			else
			{
				return $"{timeRemaining.Seconds}s";
			}
		}
		
		private Brush GetCountdownColor(DateTime eventTime)
		{
			var now = DateTime.Now;
			var localEventTime = TimeZoneInfo.ConvertTime(eventTime, TimeZoneInfo.Local);
			
			if (localEventTime <= now)
			{
				// Check if event was within the configurable recent event threshold - show urgent color for "NOW" events
				var timeSinceEvent = now - localEventTime;
				if (timeSinceEvent.TotalMinutes <= NewsRecentEventMinutes)
				{
					return NewsUrgentCountdownColor; // Event happened within recent event threshold - urgent color
				}
				return NewsTextColor; // Event has passed beyond recent threshold - normal color
			}
			
			var timeRemaining = localEventTime - now;
			
			// If less than 10 minutes, use urgent color
			if (timeRemaining.TotalMinutes < 10)
			{
				return NewsUrgentCountdownColor;
			}
			
			return NewsTextColor; // Normal color for events more than 10 minutes away
		}
		
		private PriceActionUtilsNewsEvent GetNextUpcomingEvent(List<PriceActionUtilsNewsEvent> events)
		{
			var now = DateTime.Now;
			
			// Find the next upcoming event OR the most recent event that's still within the threshold
			var upcomingEvent = events
				.Where(e => TimeZoneInfo.ConvertTime(e.Date, TimeZoneInfo.Local) > now)
				.OrderBy(e => e.Date)
				.FirstOrDefault();
				
			// If no upcoming event, check for recent events within threshold
			if (upcomingEvent == null)
			{
				var recentEvent = events
					.Where(e => 
					{
						var localEventTime = TimeZoneInfo.ConvertTime(e.Date, TimeZoneInfo.Local);
						var timeSinceEvent = now - localEventTime;
						return localEventTime <= now && timeSinceEvent.TotalMinutes <= NewsRecentEventMinutes;
					})
					.OrderByDescending(e => e.Date) // Most recent first
					.FirstOrDefault();
					
				return recentEvent;
			}
			
			return upcomingEvent;
		}
		
		private string TruncateText(string text, int maxLength)
		{
			if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
				return text;
			return text.Substring(0, maxLength - 3) + "...";
		}
		
		private async Task FetchNewsData()
		{
			try
			{
				using (var httpClient = new HttpClient())
				{
					httpClient.Timeout = TimeSpan.FromSeconds(30);
					string uuid = Guid.NewGuid().ToString();
					string url = $"https://nfs.faireconomy.media/ff_calendar_thisweek.json?version={uuid}";
					var response = await httpClient.GetStringAsync(url);
							
					// Save to temp file - using synchronous method
					File.WriteAllText(tempFilePath, response);
							
					// Parse and update news events
					ParseNewsData(response);
					Interlocked.Exchange(ref lastFetchTimeTicks, DateTime.Now.Ticks);
					
					// Use Dispatcher to safely log from background thread
					ChartControl?.Dispatcher?.BeginInvoke(new Action(() => 
						LogMessage("PriceActionUtils NewsGuard: News data fetched successfully")));
				}
			}
			catch (Exception ex)
			{
				// Use Dispatcher to safely log from background thread
				ChartControl?.Dispatcher?.BeginInvoke(new Action(() => 
					LogMessage($"PriceActionUtils NewsGuard: Error fetching news data - {ex.Message}")));
				
				// Try to fall back to cached data if available
				TryLoadCachedData();
			}
			finally
			{
				isFetchingNews = false; // Volatile write - reset the flag when done
			}
		}
		
		private void LoadNewsData()
		{
			try
			{
				if (File.Exists(tempFilePath))
				{
					TryLoadCachedData();
				}
				else
				{
					// No cached data available, start initial fetch
					StartNewsDataFetch();
				}
			}
			catch (Exception ex)
			{
				LogMessage($"PriceActionUtils NewsGuard: Error loading news data - {ex.Message}");
			}
		}
		
		private void TryLoadCachedData()
		{
			try
			{
				if (File.Exists(tempFilePath))
				{
					var json = File.ReadAllText(tempFilePath);
					ParseNewsData(json);
					var fileInfo = new FileInfo(tempFilePath);
					Interlocked.Exchange(ref lastFetchTimeTicks, fileInfo.LastWriteTime.Ticks);
					LogMessage("PriceActionUtils NewsGuard: News data loaded from cache");
				}
			}
			catch (Exception ex)
			{
				LogMessage($"PriceActionUtils NewsGuard: Error loading cached data - {ex.Message}");
			}
		}
		
		private void StartNewsDataFetch()
		{
			if (!isFetchingNews) // Simple check - volatile read
			{
				isFetchingNews = true; // Volatile write
				Task.Run(() => FetchNewsData());
			}
		}
		
		private void ParseNewsData(string json)
		{
			try
			{
				var serializer = new JavaScriptSerializer();
				var data = serializer.DeserializeObject(json) as object[];
				
				var events = new List<PriceActionUtilsNewsEvent>();
				
				if (data != null)
				{
					foreach (var item in data)
					{
						if (item is Dictionary<string, object> dict)
						{
							var newsEvent = new PriceActionUtilsNewsEvent
							{
								Title = dict.ContainsKey("title") ? dict["title"]?.ToString() ?? "" : "",
								Country = dict.ContainsKey("country") ? dict["country"]?.ToString() ?? "" : "",
								Impact = dict.ContainsKey("impact") ? dict["impact"]?.ToString() ?? "" : "",
								Forecast = dict.ContainsKey("forecast") ? dict["forecast"]?.ToString() ?? "" : "",
								Previous = dict.ContainsKey("previous") ? dict["previous"]?.ToString() ?? "" : "",
								Date = DateTime.Now // Default value
							};
							
							// Parse date
							if (dict.ContainsKey("date") && dict["date"] != null)
							{
								if (DateTime.TryParse(dict["date"].ToString(), out var parsedDate))
									newsEvent.Date = parsedDate;
							}
							
							events.Add(newsEvent);
						}
					}
				}
				
				// Atomic assignment - replace the entire list at once
				newsEvents = events; // Volatile write
			}
			catch (Exception ex)
			{
				// Use Dispatcher to safely log from background thread if available
				if (ChartControl?.Dispatcher != null)
				{
					ChartControl.Dispatcher.BeginInvoke(new Action(() => 
						LogMessage($"PriceActionUtils NewsGuard: Error parsing news data - {ex.Message}")));
				}
				else
				{
					LogMessage($"PriceActionUtils NewsGuard: Error parsing news data - {ex.Message}");
				}
			}
		}
		#endregion
		
		[Browsable(false)]
		[XmlIgnore]
		public Series<double> EMA21
		{
			get { return Values[0]; }
		}
	}
	
	public class PriceActionUtilsNewsEvent
	{
		public string Title { get; set; } = "";
		public string Country { get; set; } = "";
		public DateTime Date { get; set; }
		public string Impact { get; set; } = "";
		public string Forecast { get; set; } = "";
		public string Previous { get; set; } = "";
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private PriceActionUtils[] cachePriceActionUtils;
		public PriceActionUtils PriceActionUtils()
		{
			return PriceActionUtils(Input);
		}

		public PriceActionUtils PriceActionUtils(ISeries<double> input)
		{
			if (cachePriceActionUtils != null)
				for (int idx = 0; idx < cachePriceActionUtils.Length; idx++)
					if (cachePriceActionUtils[idx] != null &&  cachePriceActionUtils[idx].EqualsInput(input))
						return cachePriceActionUtils[idx];
			return CacheIndicator<PriceActionUtils>(new PriceActionUtils(), input, ref cachePriceActionUtils);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.PriceActionUtils PriceActionUtils()
		{
			return indicator.PriceActionUtils(Input);
		}

		public Indicators.PriceActionUtils PriceActionUtils(ISeries<double> input )
		{
			return indicator.PriceActionUtils(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.PriceActionUtils PriceActionUtils()
		{
			return indicator.PriceActionUtils(Input);
		}

		public Indicators.PriceActionUtils PriceActionUtils(ISeries<double> input )
		{
			return indicator.PriceActionUtils(input);
		}
	}
}

#endregion
