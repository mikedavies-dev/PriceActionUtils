// Copyright (C) 2025, mwwad

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

#endregion

//This namespace holds Bars types in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.BarsTypes
{
	public class SecondChartTickBarsType : BarsType
	{
		private int tickCount = 0;

		public override void ApplyDefaultBasePeriodValue(BarsPeriod period) {}

		public override void ApplyDefaultValue(BarsPeriod period) => period.Value = 2000;

		public override string ChartLabel(DateTime time) => time.ToString("HH:mm:ss");

		public override int GetInitialLookBackDays(BarsPeriod barsPeriod, TradingHours tradingHours, int barsBack) => 1;

		public override double GetPercentComplete(Bars bars, DateTime now) => bars.TickCount / (double) bars.BarsPeriod.Value;

		protected override void OnDataPoint(Bars bars, double open, double high, double low, double close, DateTime time, long volume, bool isBar, double bid, double ask)
		{
			// Increment global tick counter for this instance
			SessionIterator ??= new SessionIterator(bars);

			bool isNewSession = SessionIterator.IsNewSession(time, isBar);
			if (isNewSession)
			{
				SessionIterator.GetNextSession(time, isBar);
				tickCount = 0; // Reset tick count for new session
            }

			tickCount++;

			var offset = (int) Math.Ceiling((double)bars.BarsPeriod.Value / 2);

            // Skip ticks until offset is reached
            if (tickCount <= offset)
                return;

			int adjustedTick = tickCount - offset;
            int ticksPerBar = this.BarsPeriod.Value;

            if ((adjustedTick - 1) % ticksPerBar == 0)
				AddBar(bars, open, high, low, close, time, volume);
            else
				UpdateBar(bars, high, low, close, time, volume);
		}

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description	= @"2nd Chart tick chart by skipping a specified number of ticks";
				Name = "2nd Chart Tick";
				BarsPeriod = new BarsPeriod { BarsPeriodType = (BarsPeriodType) 17, Value = 2000 };
				// BarsPeriod		= new BarsPeriod { BarsPeriodType = BarsPeriodType.Tick };
				BuiltFrom = BarsPeriodType.Tick;
				DaysToLoad = 5;
				IsIntraday = true;
				IsTimeBased	 = false;
			}
			else if (State == State.Configure)
			{
				Name = string.Format(
					Core.Globals.GeneralOptions.CurrentCulture,
					"2nd Chart Tick {0}",
					BarsPeriod.Value,
					BarsPeriod.MarketDataType != MarketDataType.Last
						? string.Format(" - {0}", Core.Globals.ToLocalizedObject(BarsPeriod.MarketDataType, Core.Globals.GeneralOptions.CurrentUICulture))
						: string.Empty
				);

				Properties.Remove(Properties.Find("BaseBarsPeriodType",			true));
				Properties.Remove(Properties.Find("BaseBarsPeriodValue",		true));
				Properties.Remove(Properties.Find("PointAndFigurePriceType",	true));
				Properties.Remove(Properties.Find("ReversalType",				true));
				Properties.Remove(Properties.Find("Value2",						true));
			}
		}

	}
}
