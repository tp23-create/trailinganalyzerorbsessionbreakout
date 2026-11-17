using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript.DrawingTools;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class ORBSessionBreakoutV32 : Strategy
    {
        public enum SLModeType { ORBLevel, BreakoutCandle }
        public enum TPModeType { RiskReward, FixedPointsTicks, FixedPercent }
        public enum LabelSizeType { Auto, Tiny, Small, Normal, Large, Huge }
        public enum TimeZoneSelection { Local_PC_Time, IST_India, UTC, USEastern, USCentral, London }
        public enum TrailExecType { LocalMarketExit, BrokerStopModify }
        public enum TrailTriggerType { LastTradeAndBidAsk, LastTrade, LastTradeOrBidAsk }
        public enum StrongMeasureType { CandleBody, FullCandle }
        public enum TrailEngineType { TradingViewSync, LiveTick }
        public enum TvBarDetailType { High, DeepBacktest1Min, Default }
        public enum TvStraddleType { FullSubBar, AfterEntryOnly, Skip }

        #region Properties - 1. Risk Management
        [NinjaScriptProperty]
        [Range(1.0, double.MaxValue)]
        [Display(Name = "Risk Per Trade ($)", GroupName = "1. Risk Management", Order = 1)]
        public double RiskPerTrade { get; set; } = 400.0;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Contracts Limit", GroupName = "1. Risk Management", Order = 2)]
        public int MaxContracts { get; set; } = 100;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Min Contracts Default", GroupName = "1. Risk Management", Order = 3)]
        public int MinContracts { get; set; } = 1;

        [NinjaScriptProperty]
        [Display(Name = "Skip trade if 1 contract risks more than Risk Per Trade", Description = "ON: matches TradingView 'Skip Trade if Risk Too High = On'.", GroupName = "1. Risk Management", Order = 4)]
        public bool SkipOversized { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Round Contract Size to Nearest Integer", Description = "OFF (Default, Matches TradingView): Uses strict Math.Floor() just like Pine Script.", GroupName = "1. Risk Management", Order = 5)]
        public bool RoundContractSize { get; set; } = false;
        #endregion

        #region Properties - 2. Display Settings
        [NinjaScriptProperty]
        [Display(Name = "Timezone Source", GroupName = "2. Display Settings", Order = 1)]
        public TimeZoneSelection TZSelection { get; set; } = TimeZoneSelection.USEastern;

        [NinjaScriptProperty]
        [Display(Name = "Label Size", GroupName = "2. Display Settings", Order = 2)]
        public LabelSizeType LabelSize { get; set; } = LabelSizeType.Small;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Timeframe (Mins)", GroupName = "2. Display Settings", Order = 4)]
        public int MaxTimeframeMins { get; set; } = 60;

        [NinjaScriptProperty]
        [Display(Name = "Lock ORB Timeframe", GroupName = "2. Display Settings", Order = 5)]
        public bool UseOrbTfLock { get; set; } = true;

        [NinjaScriptProperty]
        [Range(1, 1440)]
        [Display(Name = "ORB Timeframe (Mins)", GroupName = "2. Display Settings", Order = 6)]
        public int OrbTfLockMinutes { get; set; } = 15;
        #endregion

        #region Properties - 3. Trade Visualization
        [NinjaScriptProperty]
        [Display(Name = "Show SL/TP Lines", GroupName = "3. Trade Visualization", Order = 1)]
        public bool ShowTradeLines { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show TP Zone", GroupName = "3. Trade Visualization", Order = 3)]
        public bool ShowTpFill { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show SL Zone", GroupName = "3. Trade Visualization", Order = 4)]
        public bool ShowSlFill { get; set; } = true;

        [NinjaScriptProperty]
        [Range(5, 100)]
        [Display(Name = "Max Trade Bars", GroupName = "3. Trade Visualization", Order = 5)]
        public int MaxTradeBars { get; set; } = 20;

        [NinjaScriptProperty]
        [Display(Name = "Highlight Breakout Candle", GroupName = "3. Trade Visualization", Order = 7)]
        public bool HighlightBreakout { get; set; } = true;

        [XmlIgnore] public Brush BreakoutCandleCol { get; set; } = Brushes.Yellow;
        [Browsable(false)] public string BreakoutCandleColSerialize { get { return Serialize.BrushToString(BreakoutCandleCol); } set { BreakoutCandleCol = Serialize.StringToBrush(value); } }
        #endregion

        #region Properties - 4. Trade Settings
        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "Max Breakouts", GroupName = "4. Trade Settings", Order = 1)]
        public int MaxBreakouts { get; set; } = 2;

        [NinjaScriptProperty]
        [Display(Name = "Stop Loss Mode", GroupName = "4. Trade Settings", Order = 2)]
        public SLModeType StopLossMode { get; set; } = SLModeType.BreakoutCandle;

        [NinjaScriptProperty]
        [Display(Name = "Close Trades on New ORB Session", Description = "ON (Matches Pine Script close_on_new_orb = true): Flattens any open trade when a new ORB session begins.", GroupName = "4. Trade Settings", Order = 3)]
        public bool CloseOnNewOrb { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Take Profit Mode", GroupName = "4. Trade Settings", Order = 4)]
        public TPModeType TakeProfitMode { get; set; } = TPModeType.RiskReward;

        [NinjaScriptProperty]
        [Range(0.5, 10.0)]
        [Display(Name = "Risk:Reward Ratio", GroupName = "4. Trade Settings", Order = 5)]
        public double RrRatio { get; set; } = 2.0;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Fixed TP (Ticks)", GroupName = "4. Trade Settings", Order = 6)]
        public int TpTicks { get; set; } = 50;

        [NinjaScriptProperty]
        [Range(0.1, 100.0)]
        [Display(Name = "Fixed TP (%)", GroupName = "4. Trade Settings", Order = 7)]
        public double TpPercent { get; set; } = 1.0;

        [NinjaScriptProperty]
        [Display(Name = "Enable Trailing Stop", GroupName = "4a. Trailing Stop (R Method)", Order = 1)]
        public bool UseTrail { get; set; } = true;

        [NinjaScriptProperty]
        [Range(0.05, 10.0)]
        [Display(Name = "Trail Activation (R-Multiple)", Description = "Same as TradingView 'Trail Activation (R-Multiple)'. Trail starts when price moves this many R from the signal close (0.4 = 0.4R).", GroupName = "4a. Trailing Stop (R Method)", Order = 2)]
        public double TrailActR { get; set; } = 0.4;

        [NinjaScriptProperty]
        [Range(0.01, 10.0)]
        [Display(Name = "Trail Offset (R-Multiple)", Description = "Same as TradingView 'Trail Offset (R-Multiple)'. Distance behind the best price in R-multiples (0.2 = 0.2R).", GroupName = "4a. Trailing Stop (R Method)", Order = 3)]
        public double TrailOffsetR { get; set; } = 0.2;

        [NinjaScriptProperty]
        [Display(Name = "Trail Exit Method", Description = "BrokerStopModify (Default): moves the resting CME exchange stop directly to the trail level. The exchange stop is the exit; a market close is used only if the stop has not reached the trail level yet.", GroupName = "4a. Trailing Stop (R Method)", Order = 6)]
        public TrailExecType TrailExecMode { get; set; } = TrailExecType.BrokerStopModify;

        [NinjaScriptProperty]
        [Range(0, 1000)]
        [Display(Name = "Backup Stop Behind Trail (Ticks)", Description = "For LocalMarketExit. Safety stop at the broker follows behind the trail level.", GroupName = "4a. Trailing Stop (R Method)", Order = 7)]
        public int BackupStopTicks { get; set; } = 20;

        [NinjaScriptProperty]
        [Display(Name = "Trail Trigger Price", Description = "LastTradeAndBidAsk (Default): Requires both trade print and live Bid/Ask for the local fallback trigger.", GroupName = "4a. Trailing Stop (R Method)", Order = 8)]
        public TrailTriggerType TrailTrigger { get; set; } = TrailTriggerType.LastTradeAndBidAsk;

        [NinjaScriptProperty]
        [Display(Name = "Filter Stray Ticks on Trail Ratchet", Description = "OFF (Default): Best price tracks the exact trade peak to match TradingView.", GroupName = "4a. Trailing Stop (R Method)", Order = 9)]
        public bool FilterRatchetStrayTicks { get; set; } = false;

        [NinjaScriptProperty]
        [Range(0, 120)]
        [Display(Name = "Trail Activation Grace Period (Sec)", Description = "0 (Default): Instant exit when trail is touched.", GroupName = "4a. Trailing Stop (R Method)", Order = 10)]
        public int TrailActivationGraceSec { get; set; } = 0;

        [NinjaScriptProperty]
        [Range(0, 10000)]
        [Display(Name = "Trail Breach Confirmation (ms)", Description = "0 (Default): Instant exit on first confirmed touch of trail level.", GroupName = "4a. Trailing Stop (R Method)", Order = 11)]
        public int TrailConfirmMs { get; set; } = 0;

        [NinjaScriptProperty]
        [Display(Name = "Protect 0.3R Activation From Entry Slippage", Description = "ON (v2.6 Default): Keeps 1R & 0.2R offset locked to TradingView's signal bar, but requires price to clear actual fill + 0.3R if entry chased. OFF: pure signal-close activation.", GroupName = "4a. Trailing Stop (R Method)", Order = 12)]
        public bool ProtectSlippageOnTrailAct { get; set; } = false;

        [NinjaScriptProperty]
        [Range(0, 50)]
        [Display(Name = "Broker Stop Breach Grace (Ticks)", Description = "v2.6: In BrokerStopModify mode, lets the resting exchange stop fill natively unless price breaches currentSL and the resting stop is more than this many ticks behind.", GroupName = "4a. Trailing Stop (R Method)", Order = 13)]
        public int BrokerStopGraceTicks { get; set; } = 3;

        [NinjaScriptProperty]
        [Range(0, 2000)]
        [Display(Name = "Broker Stop Modify Wait (ms)", Description = "v2.6: Minimum wait after sending a stop modification before local market-breach backup can cancel and replace it (prevents cancelling a stop mid-ratchet).", GroupName = "4a. Trailing Stop (R Method)", Order = 14)]
        public int BrokerStopSyncWaitMs { get; set; } = 250;

        [NinjaScriptProperty]
        [Display(Name = "Log TradingView Shadow Audit", Description = "ON (v2.6 Default): Logs side-by-side comparison of your live exit vs TradingView's OHLC Broker Emulator to show exact tick differences.", GroupName = "4a. Trailing Stop (R Method)", Order = 15)]
        public bool LogTvShadow { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Fast Entry: Timer Close", Description = "ON: Evaluates breakout at the moment the candle closes (PC clock).", GroupName = "4b. Fast Entry (Timer Close)", Order = 1)]
        public bool UseTimerClose { get; set; } = true;

        [NinjaScriptProperty]
        [Range(0, 2000)]
        [Display(Name = "Timer Delay After Close (ms)", Description = "0 ms (v2.5.2 Default): checks the breakout the moment the candle closes. If the last trades of the candle arrive late, the trade is re-anchored to the final bar anyway.", GroupName = "4b. Fast Entry (Timer Close)", Order = 2)]
        public int TimerDelayMs { get; set; } = 15;

        [NinjaScriptProperty]
        [Display(Name = "Re-anchor to Final Bar (match TradingView)", Description = "ON: on the first tick of the next bar, SL / 1R / trail activation / trail offset are recomputed from the FINAL signal-bar close, high and low - the exact values TradingView uses.", GroupName = "4b. Fast Entry (Timer Close)", Order = 3)]
        public bool ReanchorToFinal { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Close Trade if Final Bar Cancels Signal", Description = "ON: if the final signal-bar values would NOT have produced this trade in TradingView (close back inside ORB, stop too small, 1-contract risk too high), the position is closed at once.", GroupName = "4b. Fast Entry (Timer Close)", Order = 4)]
        public bool PhantomExit { get; set; } = true;

        [NinjaScriptProperty]
        [Range(0, 5000)]
        [Display(Name = "Entry at Signal Close Window (ms)", Description = "v2.5.2: above 0 = the entry is first a LIMIT at the exact signal close (TradingView's entry price) for this many ms. If not filled, it is moved through the market so the trade is never missed. 0 = plain market entry (v2.5.1 behaviour).", GroupName = "4b. Fast Entry (Timer Close)", Order = 5)]
        public int EntryAtCloseMs { get; set; } = 300;

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "Entry Chase Limit (Ticks)", Description = "After the window, the entry limit is moved this many ticks past the bid/ask: it fills at the best available price, never worse than this.", GroupName = "4b. Fast Entry (Timer Close)", Order = 6)]
        public int EntryChaseTicks { get; set; } = 20;

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Chase Early If Price Runs (Ticks)", Description = "While waiting at the signal close price: if the market runs this many ticks away (ask above close for buy, bid below close for sell), chase immediately instead of waiting the full window. 0 = off.", GroupName = "4b. Fast Entry (Timer Close)", Order = 7)]
        public int ChaseRunTicks { get; set; } = 2;

        [NinjaScriptProperty]
        [Display(Name = "Enter on Strong 1st Candle After ORB", GroupName = "4c. Strong 1st Candle After ORB", Order = 1)]
        public bool UseStrongFirst { get; set; } = true;

        [NinjaScriptProperty]
        [Range(1.0, 100.0)]
        [Display(Name = "Strong Candle % Beyond ORB", GroupName = "4c. Strong 1st Candle After ORB", Order = 2)]
        public double StrongFirstPct { get; set; } = 50.0;

        [NinjaScriptProperty]
        [Display(Name = "Measure Strength By", GroupName = "4c. Strong 1st Candle After ORB", Order = 3)]
        public StrongMeasureType StrongFirstMode { get; set; } = StrongMeasureType.CandleBody;

        [NinjaScriptProperty]
        [Display(Name = "Show 1st Candle Strength Label", GroupName = "4c. Strong 1st Candle After ORB", Order = 4)]
        public bool ShowStrongInfo { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Skip 1st Candle in Midnight & London (00:30 & 03:30)", Description = "ON (v3.0 Default): Waits for the 2nd candle (00:45 & 03:45) in Midnight (00:15) and London (03:15) sessions where 1st-bar stop-hunts lost -$2,197/yr, while keeping Strong 1st Candle ON for NY sessions (06:15, 08:00, 09:30).", GroupName = "4c. Strong 1st Candle After ORB", Order = 5)]
        public bool SkipOvernight1stBar { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Trail Engine", Description = "TradingViewSync (v3.2 Default) = the trail is decided exactly like TradingView's backtest: on each finished sub-bar, price is walked Open -> nearer extreme -> other extreme -> Close. The broker stop only moves when a sub-bar closes, and when TradingView's trade exits inside a sub-bar the live trade is closed at that sub-bar's close. LiveTick = plain tick-by-tick trail (does NOT match TradingView).", GroupName = "4d. Trail Engine (TradingView Sync)", Order = 1)]
        public TrailEngineType TrailEngine { get; set; } = TrailEngineType.TradingViewSync;

        [NinjaScriptProperty]
        [Display(Name = "TradingView Bar Detalization", Description = "High (Default) = TradingView 'Bar detalization: High' (2-min sub-bars on a 15m chart, 30-sec on 5m, 10-sec on 1m). DeepBacktest1Min = 1-min sub-bars. Default = full chart candle OHLC (4 ticks per bar).", GroupName = "4d. Trail Engine (TradingView Sync)", Order = 2)]
        public TvBarDetailType TvBarDetail { get; set; } = TvBarDetailType.High;

        [NinjaScriptProperty]
        [Display(Name = "Sub-Bar Crossing a Candle Border", Description = "Only matters when the sub-bar does not divide the candle evenly (15m chart / 2-min sub-bars: the 14-16 and 44-46 sub-bars cross the candle border). FullSubBar (Default) = TradingView rule: the sub-bar belongs to the candle that holds its last minute and is walked with its full Open/High/Low/Close, after a print of the candle open. AfterEntryOnly = only the minute after the entry is used. Skip = that sub-bar is ignored for a trade entered at the border.", GroupName = "4d. Trail Engine (TradingView Sync)", Order = 3)]
        public TvStraddleType TvStraddle { get; set; } = TvStraddleType.FullSubBar;

        [NinjaScriptProperty]
        [Display(Name = "Wait While TradingView Trade Still Open", Description = "ON (Default): If our stop closes the trade mid-bar while TradingView's trade is still open, wait until TradingView's trade closes before taking a new entry.", GroupName = "4d. Trail Engine (TradingView Sync)", Order = 4)]
        public bool TvShadowBlock { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Remove Hard TP when Trailing", GroupName = "4. Trade Settings", Order = 11)]
        public bool TrailRmLimit { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Rebase SL/TP to Actual Fill", Description = "OFF (Default, Matches TradingView with 0 slippage): SL, 1R and trail levels are locked to the Signal Candle Close & Wick.", GroupName = "4. Trade Settings", Order = 12)]
        public bool RebaseToFill { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Log Timing to Output Window", GroupName = "4. Trade Settings", Order = 13)]
        public bool LogTiming { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Flatten Before Close (Prop Firm)", GroupName = "4. Trade Settings", Order = 14)]
        public bool UseFlatten { get; set; } = true;

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Flatten Time (New York)", Description = "16:45 matches TradingView 5m bar-close on the 16:40 EOD window.", GroupName = "4. Trade Settings", Order = 15)]
        public DateTime FlattenTime { get; set; } = DateTime.Parse("16:45");

        [NinjaScriptProperty]
        [Range(0, 400)]
        [Display(Name = "Max Entry Slippage (Ticks, 0 = Market)", Description = "Only used when 'Entry at Signal Close Window' is 0: a capped limit entry that is cancelled after the timeout.", GroupName = "4. Trade Settings", Order = 16)]
        public int MaxEntrySlipTicks { get; set; } = 0;

        [NinjaScriptProperty]
        [Range(1, 60)]
        [Display(Name = "Entry Limit Timeout (Sec)", GroupName = "4. Trade Settings", Order = 17)]
        public int EntryLimitTimeoutSec { get; set; } = 5;

        [NinjaScriptProperty]
        [Range(1, 40)]
        [Display(Name = "Stop Safety Margin (Ticks)", Description = "1 tick (Default) allows the exchange stop to sit tight on the live market.", GroupName = "4. Trade Settings", Order = 18)]
        public int StopSafetyTicks { get; set; } = 1;
        #endregion

        #region Properties - 5. ORB Session
        [NinjaScriptProperty]
        [Display(Name = "Show ORB Box", GroupName = "5. ORB Session", Order = 1)]
        public bool ShowOrbBox { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Use Asia", GroupName = "5. ORB Session", Order = 2)]
        public bool UseAsia { get; set; } = true;

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Asia Start Time", GroupName = "5. ORB Session", Order = 3)]
        public DateTime AsiaSessionStart { get; set; } = DateTime.Parse("00:15");

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Asia End Time", GroupName = "5. ORB Session", Order = 4)]
        public DateTime AsiaSessionEnd { get; set; } = DateTime.Parse("00:30");

        [NinjaScriptProperty]
        [Display(Name = "Use London", GroupName = "5. ORB Session", Order = 5)]
        public bool UseLondon { get; set; } = true;

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "London Start Time", GroupName = "5. ORB Session", Order = 6)]
        public DateTime LondonSessionStart { get; set; } = DateTime.Parse("03:15");

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "London End Time", GroupName = "5. ORB Session", Order = 7)]
        public DateTime LondonSessionEnd { get; set; } = DateTime.Parse("03:30");

        [NinjaScriptProperty]
        [Display(Name = "Use NY", GroupName = "5. ORB Session", Order = 8)]
        public bool UseNy { get; set; } = true;

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "NY Start Time", GroupName = "5. ORB Session", Order = 9)]
        public DateTime NySessionStart { get; set; } = DateTime.Parse("09:15");

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "NY End Time", GroupName = "5. ORB Session", Order = 10)]
        public DateTime NySessionEnd { get; set; } = DateTime.Parse("09:30");

        [NinjaScriptProperty]
        [Display(Name = "Use Slot 4", GroupName = "5. ORB Session", Order = 11)]
        public bool UseExtra { get; set; } = true;

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Slot 4 Start Time", GroupName = "5. ORB Session", Order = 12)]
        public DateTime ExtraSessionStart { get; set; } = DateTime.Parse("07:45");

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Slot 4 End Time", GroupName = "5. ORB Session", Order = 13)]
        public DateTime ExtraSessionEnd { get; set; } = DateTime.Parse("08:00");

        [NinjaScriptProperty]
        [Display(Name = "Use Slot 5", GroupName = "5. ORB Session", Order = 14)]
        public bool UseSlot5 { get; set; } = true;

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Slot 5 Start Time", GroupName = "5. ORB Session", Order = 15)]
        public DateTime Slot5SessionStart { get; set; } = DateTime.Parse("06:00");

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Slot 5 End Time", GroupName = "5. ORB Session", Order = 16)]
        public DateTime Slot5SessionEnd { get; set; } = DateTime.Parse("06:15");

        [NinjaScriptProperty]
        [Display(Name = "Slot 5 (NY 06:00) T1 Only (Skip T2 Reversion)", Description = "ON (v3.0 Default): Takes T1 in NY 06:00 (+$9,395/yr) and skips T2 reversions in NY 06:00 (-$3,356/yr loss ahead of 07:45 ORB).", GroupName = "5. ORB Session", Order = 17)]
        public bool Ny0600T1Only { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Use Slot 6", Description = "OFF (v3.0 Default): Asia 20:00-20:15 made only $3.56/trade over 295 trades with -$30k gross losses.", GroupName = "5. ORB Session", Order = 18)]
        public bool UseSlot6 { get; set; } = true;

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Slot 6 Start Time", GroupName = "5. ORB Session", Order = 19)]
        public DateTime Slot6SessionStart { get; set; } = DateTime.Parse("20:00");

        [PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
        [Display(Name = "Slot 6 End Time", GroupName = "5. ORB Session", Order = 20)]
        public DateTime Slot6SessionEnd { get; set; } = DateTime.Parse("20:15");

        [NinjaScriptProperty]
        [Range(1, 12)]
        [Display(Name = "Tracking Hours", GroupName = "5. ORB Session", Order = 21)]
        public int TrackingHours { get; set; } = 2;

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Box Transparency", GroupName = "5. ORB Session", Order = 22)]
        public int BoxTransparency { get; set; } = 80;
        #endregion

        #region Properties - 6. Pivot Lines
        [NinjaScriptProperty]
        [Display(Name = "Show High/Low Lines", GroupName = "6. Pivot Lines", Order = 1)]
        public bool ShowPivots { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show 50% Level", GroupName = "6. Pivot Lines", Order = 2)]
        public bool Show50Level { get; set; } = true;
        #endregion

        #region Properties - 7. Trade Filters
        [NinjaScriptProperty]
        [Display(Name = "Enable Day Filter", GroupName = "7. Trade Filters", Order = 1)]
        public bool UseDayFilter { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Trade Monday", GroupName = "7. Trade Filters", Order = 2)]
        public bool TradeMonday { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Trade Tuesday", GroupName = "7. Trade Filters", Order = 3)]
        public bool TradeTuesday { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Trade Wednesday", GroupName = "7. Trade Filters", Order = 4)]
        public bool TradeWednesday { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Trade Thursday", GroupName = "7. Trade Filters", Order = 5)]
        public bool TradeThursday { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Trade Friday", GroupName = "7. Trade Filters", Order = 6)]
        public bool TradeFriday { get; set; } = true;
        #endregion

        private double orbHigh = double.NaN;
        private double orbLow = double.NaN;
        private DateTime orbStartTime = DateTime.MinValue;
        private int activeSessionId = 0; // 1=Asia(00:15), 2=London(03:15), 3=NY(09:15), 4=Slot4(07:45), 5=Slot5(06:00), 6=Slot6(20:00)
        private bool orbActive = false;
        private bool orbComplete = false;
        private bool trackingExpired = false;

        private int breakoutCount = 0;
        private double currentEntry = double.NaN;
        private double initialSL = double.NaN;
        private double currentSL = double.NaN;
        private double currentTP = double.NaN;
        private bool currentIsLong = false;
        private int lastTradeBar = -1;
        private int tradeStartBar = -1;
        private bool stopPlaced = false;
        private bool needLineRedraw = false;
        private int entryFilledQty = 0;
        private double entryFillValue = 0;
        private double plannedSlSize = 0;
        private double plannedEntry = double.NaN;
        private DateTime lastFlattenAttempt = DateTime.MinValue;
        private bool trailActivated = false;
        private DateTime trailActivatedAt = DateTime.MinValue;
        private DateTime trailBreachStart = DateTime.MinValue;
        private Order entryOrder = null;
        private Order stopOrder = null;
        private Order tpOrder = null;
        private bool entryStopSent = false;
        private bool stopSyncPending = false;
        private bool tpSyncPending = false;
        private DateTime lastStopSubmit = DateTime.MinValue;
        private DateTime lastStopChangeError = DateTime.MinValue;
        private bool emergencyFlattenPending = false;
        private DateTime lastEmergencyFlatten = DateTime.MinValue;
        private DateTime entryLimitSentAt = DateTime.MinValue;

        private double backupSL = double.NaN;
        private Order closeOrder = null;
        private DateTime lastCloseSent = DateTime.MinValue;
        private bool closingTrade = false;
        private string closeName = "";
        private bool realtimeTradeSeen = false;
        private DateTime lastStrayAttempt = DateTime.MinValue;
        private int strayAttempts = 0;

        private bool exitCancelSent = false;
        private DateTime exitCancelSentAt = DateTime.MinValue;
        private DateTime restingExitFilledAt = DateTime.MinValue;
        private bool tradingHalted = false;

        private int timerTradeBar = -1;
        private double plannedSL = double.NaN;
        private double entryOrbHigh = double.NaN;
        private double entryOrbLow = double.NaN;
        private int plannedQty = 0;
        private DateTime orbEndBarClose = DateTime.MinValue;
        private int prevBreakoutCount = 0;
        private bool prevHadBreakoutHigh = false;
        private bool prevHadBreakoutLow = false;
        private int prevLastTradeBar = -1;
        private bool prevIsLong = false;
        private bool restoreIsLongWhenFlat = false;

        private bool entryAtClosePhase = false;
        private DateTime entryAtCloseSentAt = DateTime.MinValue;

        private bool postExitWatch = false;
        private double postExitHi = double.MinValue;
        private double postExitLo = double.MaxValue;
        private double lastExitPrice = double.NaN;
        private double lastExitSL = double.NaN;
        private double lastExitBestPrice = double.NaN;

        private System.Threading.Timer entryTimer = null;
        private readonly object timerLock = new object();
        private DateTime timerArmedFor = DateTime.MinValue;
        private DateTime timerEvaluatedClose = DateTime.MinValue;
        private bool timerWarned = false;

        private double highestPriceSinceEntry = double.MinValue;
        private double lowestPriceSinceEntry = double.MaxValue;
        private DateTime liveTradeStartTime = DateTime.MinValue;

        private bool hadBreakoutHigh = false;
        private bool hadBreakoutLow = false;

        // v2.7: Pine Script T1 SL-Hit Re-Entry State Variables & Same-Bar Reversion Queue
        private bool t1SlHit = false;
        private double t1SlLevel = double.NaN;
        private bool t1WasLong = false;
        private bool prevT1SlHit = false;
        private double prevT1SlLevel = double.NaN;
        private bool prevT1WasLong = false;

        private bool pendingT2Reversion = false;
        private bool pendingT2IsLong = false;
        private double pendingT2OrbHigh = double.NaN;
        private double pendingT2OrbLow = double.NaN;
        private int pendingT2Offset = 0;
        private DateTime pendingT2QueuedAt = DateTime.MinValue;

        private double effOrbHigh = double.NaN;
        private double effOrbLow = double.NaN;
        private bool effOrbReady = false;

        private bool wasInOrb = false;
        private bool wasInOrbBeforeBar = false;
        private DateTime lastEvaluatedBarClose = DateTime.MinValue;

        private bool tvSimActive = false, tvSimActivated = false, tvSimExited = false, tvShadow = false;
        private double tvSimBest = double.NaN, tvSimLastPx = double.NaN, tvLastExitPx = double.NaN;
        private DateTime tvSimLastBarTime = DateTime.MinValue, tvSimStart = DateTime.MinValue, tvChartCloseDone = DateTime.MinValue;
        private int tvExitReason = 0;
        private string tvExitDesc = "";

        // v3.2 TradingView sub-bar feed (built from exchange bar times, never from the PC clock)
        private int orbBip = -1;            // extra series used for the locked ORB timeframe (-1 = none)
        private int tvBip = -1;             // extra series the TradingView sub-bars are built from (-1 = chart candle is the sub-bar)
        private int chartMinutes = 0;       // chart timeframe in minutes
        private int tvSubSec = 0;           // TradingView sub-bar length in seconds
        private int tvBaseSec = 0;          // length of one bar of the tvBip series in seconds
        private int tvAgg = 1;              // tvBip bars per TradingView sub-bar (2 on a 15m chart: 1-min bars -> 2-min sub-bars)
        private int tvLastBaseIdx = -1;
        private bool tvPendHas = false;
        private long tvPendIdx = -1;
        private double tvPO = double.NaN, tvPH = double.NaN, tvPL = double.NaN, tvPC = double.NaN, tvPendChartOpen = double.NaN;
        private DateTime tvPendOpenTime = DateTime.MinValue;
        private static readonly DateTime tvAnchor = new DateTime(2000, 1, 2, 18, 0, 0);   // a Sunday 18:00 New York = CME session open
        private string lastExitName = "";
        private bool brokerStopAtTvLevel = false, tvLevelLogged = false;
        private DateTime lastExitExecAt = DateTime.MinValue;
        private bool wasMtfAsia = false;
        private bool wasMtfLondon = false;
        private bool wasMtfNy = false;
        private bool wasMtfExtra = false;
        private bool wasMtfS5 = false;
        private bool wasMtfS6 = false;

        private double mtfAsiaHigh = double.NaN, mtfAsiaLow = double.NaN;
        private double mtfLondonHigh = double.NaN, mtfLondonLow = double.NaN;
        private double mtfNyHigh = double.NaN, mtfNyLow = double.NaN;
        private double mtfExtraHigh = double.NaN, mtfExtraLow = double.NaN;
        private double mtfS5High = double.NaN, mtfS5Low = double.NaN;
        private double mtfS6High = double.NaN, mtfS6Low = double.NaN;

        private TimeZoneInfo nyZone;
        private TimeZoneInfo sourceZoneCached;

        private bool LocalTrail { get { return TrailExecMode == TrailExecType.LocalMarketExit; } }
        private bool TvEngine { get { return TrailEngine == TrailEngineType.TradingViewSync && UseTrail; } }
        private bool TvShadowOpen() { return TvEngine && TvShadowBlock && tvShadow && tvSimActive; }
        private double TvRefEntry() { return (!double.IsNaN(plannedEntry) && plannedEntry > 0) ? plannedEntry : currentEntry; }

        private double TrailRefEntry()
        {
            if (!RebaseToFill && !double.IsNaN(plannedEntry) && plannedEntry > 0)
                return plannedEntry;
            return currentEntry;
        }

        private double TrailActRefEntry()
        {
            double baseRef = TrailRefEntry();
            if (ProtectSlippageOnTrailAct && !RebaseToFill && entryFilledQty > 0 && !double.IsNaN(currentEntry) && currentEntry > 0)
            {
                return currentIsLong ? Math.Max(baseRef, currentEntry) : Math.Min(baseRef, currentEntry);
            }
            return baseRef;
        }

        private double TrailRefSlSize()
        {
            if (!RebaseToFill && plannedSlSize > 0)
                return Math.Max(TickSize, plannedSlSize);
            return Math.Max(TickSize, Math.Abs(currentEntry - initialSL));
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "ORB Session Breakout 3.2 R-Trail (TradingView-exact trail: clock-aligned sub-bars, stop moves only on a finished sub-bar, exit at the sub-bar close when TradingView exits)";
                Name = "ORB Session Breakout 3.2 TV-Exact Trail";
                IgnoreOverfill = true;
                Calculate = Calculate.OnEachTick;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = false;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.IgnoreAllErrors;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
            }
            else if (State == State.Configure)
            {
                orbBip = -1; tvBip = -1; tvBaseSec = 0; tvAgg = 1;
                chartMinutes = BarsPeriod.Value;
                if (UseOrbTfLock && OrbTfLockMinutes != BarsPeriod.Value)
                {
                    AddDataSeries(Data.BarsPeriodType.Minute, OrbTfLockMinutes);
                    orbBip = 1;
                }

                // v3.2: series the TradingView sub-bars are built from
                tvSubSec = TvIntrabarSeconds();
                if (TvEngine && BarsPeriod.BarsPeriodType == Data.BarsPeriodType.Minute && tvSubSec > 0 && tvSubSec < chartMinutes * 60)
                {
                    if ((chartMinutes * 60) % tvSubSec == 0 || tvSubSec % 60 != 0) { tvBaseSec = tvSubSec; tvAgg = 1; }
                    else { tvBaseSec = 60; tvAgg = tvSubSec / 60; }
                    if (tvBaseSec % 60 == 0) AddDataSeries(Data.BarsPeriodType.Minute, tvBaseSec / 60);
                    else AddDataSeries(Data.BarsPeriodType.Second, tvBaseSec);
                    tvBip = orbBip > 0 ? 2 : 1;
                }

                try { nyZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
                catch { nyZone = TimeZoneInfo.Utc; }
                sourceZoneCached = ResolveSourceZone();
            }
            else if (State == State.DataLoaded)
            {
                if (LogTiming)
                {
                    OrbLog(string.Format("[ORB] SETTINGS v3.2 chart {0}-min | ORB lock {1} ({2} min) | risk ${3} (round qty {4}) | max {5} contracts | trail {6} act {7}R / behind {8}R / trigger {9} / ratchetFilter {10} | trail exit {11} (backup {12}t) | rebase {13} | flatten {14} at {15:HH:mm} | timezone {16}",
                        BarsPeriod.Value, UseOrbTfLock, OrbTfLockMinutes, RiskPerTrade, RoundContractSize, MaxContracts, UseTrail ? "ON" : "OFF", TrailActR, TrailOffsetR, TrailTrigger, FilterRatchetStrayTicks, TrailExecMode, BackupStopTicks, RebaseToFill, UseFlatten, FlattenTime, TZSelection));
                    OrbLog(string.Format("[ORB] SETTINGS fast entry timer close {0} | timer delay {1} ms | re-anchor {2} | phantom exit {3} | entry at close {4} (chase {5}t, early chase {6}t) | strong1st {7} ({8}% {9}) | protectActSlippage {10} | brokerGrace {11}t / {12}ms | tvShadow {13}",
                        UseTimerClose ? "ON" : "OFF", TimerDelayMs, ReanchorToFinal ? "ON" : "OFF", PhantomExit ? "ON" : "OFF",
                        EntryAtCloseMs > 0 ? EntryAtCloseMs + " ms" : "OFF (market)", EntryChaseTicks, ChaseRunTicks,
                        UseStrongFirst ? "ON" : "OFF", StrongFirstPct, StrongFirstMode,
                        ProtectSlippageOnTrailAct ? "ON" : "OFF", BrokerStopGraceTicks, BrokerStopSyncWaitMs, LogTvShadow ? "ON" : "OFF"));
                    int tvSec = TvIntrabarSeconds();
                    OrbLog(string.Format("[ORB] SETTINGS trail engine {0} | TradingView bar detalization {1} ({2}) | sub-bars built from {3} | border sub-bar rule {4} | skipOvernight1stBar {5} | ny0600T1Only {6} | useSlot6(20:00) {7}",
                        TvEngine ? "TRADINGVIEW-EXACT v3.2" : (UseTrail ? "LIVE-TICK" : "OFF"), TvBarDetail,
                        (tvSec <= 0 || tvSec >= BarsPeriod.Value * 60) ? "chart candle (OHLC)" : (tvSec >= 60 ? (tvSec / 60) + "-min sub-bar" : tvSec + "-sec sub-bar"),
                        tvBip > 0 ? (tvBaseSec >= 60 ? (tvBaseSec / 60) + "-min" : tvBaseSec + "-sec") + " exchange bars (x" + tvAgg + ", clock-aligned)" : "the chart candle",
                        TvStraddle,
                        SkipOvernight1stBar ? "ON" : "OFF", Ny0600T1Only ? "ON" : "OFF", UseSlot6 ? "ON" : "OFF"));
                }
            }
            else if (State == State.Realtime)
            {
                lastHistoricalBar = CurrentBars[0];

                if (UseTimerClose)
                {
                    if (BarsPeriod.BarsPeriodType == Data.BarsPeriodType.Minute)
                        entryTimer = new System.Threading.Timer(OnEntryTimer, null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                    else
                        OrbLog("[ORB] NOTE | Fast Entry Timer Close works on minute charts only -> using normal first-tick entry");
                }
            }
            else if (State == State.Terminated)
            {
                if (entryTimer != null)
                {
                    try { entryTimer.Dispose(); } catch { }
                    entryTimer = null;
                }
            }
        }

        private void ArmEntryTimer()
        {
            if (entryTimer == null) return;
            DateTime closeT = Time[0];
            if (closeT == timerArmedFor) return;

            DateTime closeLocal;
            try { closeLocal = TimeZoneInfo.ConvertTime(closeT, sourceZoneCached ?? ResolveSourceZone(), TimeZoneInfo.Local); }
            catch { return; }

            double toCloseMs = (closeLocal - DateTime.Now).TotalMilliseconds;
            if (toCloseMs < -2000 || toCloseMs > BarsPeriod.Value * 60000.0 + 2000)
            {
                if (!timerWarned)
                {
                    timerWarned = true;
                    OrbLog(string.Format("[ORB] NOTE | Timer Close skipped: candle end {0:HH:mm:ss} is {1:0} ms from PC clock. Normal first-tick entry is used.", closeT, toCloseMs));
                }
                return;
            }

            lock (timerLock) { timerArmedFor = closeT; }
            try { entryTimer.Change((long)Math.Max(0, toCloseMs + TimerDelayMs), System.Threading.Timeout.Infinite); } catch { }
        }

        private void OnEntryTimer(object s)
        {
            try
            {
                if (State != State.Realtime) return;
                DateTime closeT;
                lock (timerLock) { closeT = timerArmedFor; }
                TriggerCustomEvent(OnTimerClose, 0, closeT);
            }
            catch { }
        }

        private void OnTimerClose(object state)
        {
            if (State != State.Realtime || !UseTimerClose || state == null) return;
            if (CurrentBar < BarsRequiredToTrade || CurrentBar < 2 || BarsPeriod.Value > MaxTimeframeMins) return;
            DateTime closeT = (DateTime)state;
            if (Time[0] != closeT) return;
            if (timerEvaluatedClose == closeT) return;
            timerEvaluatedClose = closeT;
            if (TvEngine && tvBip > 0)
            {
                TvChartBarClosed(closeT, Close[0]);
                TvActOnSim();
            }
            EvaluateBarCloseLogic(0, true);
        }

        private TimeZoneInfo ResolveSourceZone()
        {
            switch (TZSelection)
            {
                case TimeZoneSelection.IST_India: try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); } catch { return TimeZoneInfo.Local; }
                case TimeZoneSelection.UTC: return TimeZoneInfo.Utc;
                case TimeZoneSelection.USEastern: try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); } catch { return TimeZoneInfo.Local; }
                case TimeZoneSelection.USCentral: try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); } catch { return TimeZoneInfo.Local; }
                case TimeZoneSelection.London: try { return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"); } catch { return TimeZoneInfo.Local; }
                case TimeZoneSelection.Local_PC_Time:
                default: return TimeZoneInfo.Local;
            }
        }

        private DateTime GetNYTime(DateTime sourceTime)
        {
            TimeZoneInfo sourceZone = sourceZoneCached ?? ResolveSourceZone();
            try
            {
                DateTime utc = TimeZoneInfo.ConvertTime(sourceTime, sourceZone, TimeZoneInfo.Utc);
                return TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.Utc, nyZone);
            }
            catch { return sourceTime; }
        }

        private bool PassesFilters(DateTime nyTime)
        {
            if (!UseDayFilter) return true;
            DayOfWeek dow = nyTime.DayOfWeek;
            switch (dow)
            {
                case DayOfWeek.Monday: return TradeMonday;
                case DayOfWeek.Tuesday: return TradeTuesday;
                case DayOfWeek.Wednesday: return TradeWednesday;
                case DayOfWeek.Thursday: return TradeThursday;
                case DayOfWeek.Friday: return TradeFriday;
                default: return false;
            }
        }

        private bool IsInSessionStrict(int barMins, int startMins, int endMins)
        {
            if (startMins <= endMins) return barMins >= startMins && barMins < endMins;
            else return barMins >= startMins || barMins < endMins;
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution == null || execution.Order == null) return;
            if (execution.Order.Name != "ORB_Entry")
            {
                if (quantity > 0)
                {
                    lastExitExecAt = DateTime.Now;
                    restingExitFilledAt = DateTime.Now;
                }
                if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled)
                {
                    if (execution.Order.OrderState == OrderState.Filled)
                        closingTrade = true;
                    lastExitName = execution.Order.Name;
                    lastExitPrice = price;
                    lastExitSL = currentSL;
                    lastExitBestPrice = currentIsLong ? highestPriceSinceEntry : lowestPriceSinceEntry;

                    // v2.7: Pine Script T1 SL-Hit detection for T2 reversion gate
                    if (breakoutCount == 1)
                    {
                        bool isInitialSlExit = !trailActivated && (execution.Order.Name == "ORB_SL" || execution.Order.Name == "ORB_TV_SL" || execution.Order.Name == "Stop Gap Exit");
                        t1SlHit = isInitialSlExit;
                    }
                    if (State == State.Realtime && !postExitWatch)
                    {
                        postExitWatch = true;
                        postExitHi = High[0];
                        postExitLo = Low[0];
                    }

                    if (LogTiming)
                    {
                        double best = currentIsLong ? highestPriceSinceEntry : lowestPriceSinceEntry;
                        double actRef = TrailActRefEntry();
                        double actLevel = currentIsLong ? actRef + TrailActTicks() * TickSize : actRef - TrailActTicks() * TickSize;
                        double profitPts = currentIsLong ? price - currentEntry : currentEntry - price;
                        double tvPts = currentIsLong ? price - plannedEntry : plannedEntry - price;
                        OrbLog(string.Format("[ORB] EXIT  {0:HH:mm:ss.fff} PC | {1} x{2} @ {3} | stop/trail level was {4} (fill vs level {5:0.##} pts) | best price since entry {6} | trail activation level {7} | trail {8} | result {9:0.##} pts | vs signal close (TV entry) {10:0.##} pts",
                            DateTime.Now, execution.Order.Name, quantity, price, currentSL, Math.Abs(price - currentSL), best, actLevel, trailActivated ? "was ON" : "never activated", profitPts, tvPts));

                        if (LogTvShadow && !double.IsNaN(plannedEntry) && plannedEntry > 0)
                        {
                            double entrySlipTicks = (currentIsLong ? (currentEntry - plannedEntry) : (plannedEntry - currentEntry)) / TickSize;
                            double exitSlipTicks = (currentIsLong ? (currentSL - price) : (price - currentSL)) / TickSize;
                            double tvIdealExit = currentSL;
                            double liveTicks = profitPts / TickSize;
                            double tvIdealAtExitMomentTicks = (currentIsLong ? (tvIdealExit - plannedEntry) : (plannedEntry - tvIdealExit)) / TickSize;
                            OrbLog(string.Format("[ORB] TV MATCH AUDIT (AT EXIT) | Live: {0:0.#}t (entry {1} -> exit {2}) | TV Zero-Slippage at same peak ({3}): {4:0.#}t (signal close {5} -> trail SL {6}) | Diff breakdown: entry fill slip {7:+0.#;-0.#;0}t, exit stop fill slip {8:+0.#;-0.#;0}t",
                                liveTicks, currentEntry, price, best, tvIdealAtExitMomentTicks, plannedEntry, tvIdealExit, entrySlipTicks, exitSlipTicks));
                        }
                    }
                }
                return;
            }
            if (execution.Order.OrderState != OrderState.Filled && execution.Order.OrderState != OrderState.PartFilled) return;
            if (double.IsNaN(initialSL) || plannedSlSize <= 0) return;

            entryFilledQty += quantity;
            entryFillValue += price * quantity;
            currentEntry = Instrument.MasterInstrument.RoundToTickSize(entryFillValue / entryFilledQty);

            if (!entryStopSent)
            {
                double startRef = TrailRefEntry();
                highestPriceSinceEntry = currentIsLong ? Math.Max(startRef, currentEntry) : currentEntry;
                lowestPriceSinceEntry = !currentIsLong ? Math.Min(startRef, currentEntry) : currentEntry;
            }
            else
            {
                highestPriceSinceEntry = Math.Max(highestPriceSinceEntry, price);
                lowestPriceSinceEntry = Math.Min(lowestPriceSinceEntry, price);
            }

            if (RebaseToFill)
            {
                double slDist = Math.Max(TickSize, plannedSlSize);
                double rebased = (marketPosition == MarketPosition.Long) ? currentEntry - slDist : currentEntry + slDist;
                initialSL = Instrument.MasterInstrument.RoundToTickSize(rebased);
                currentSL = initialSL;
            }

            double refEntryForTp = TrailRefEntry();
            double slSize = TrailRefSlSize();
            if (TakeProfitMode == TPModeType.RiskReward) currentTP = currentIsLong ? refEntryForTp + (slSize * RrRatio) : refEntryForTp - (slSize * RrRatio);
            else if (TakeProfitMode == TPModeType.FixedPointsTicks) currentTP = currentIsLong ? refEntryForTp + (TpTicks * TickSize) : refEntryForTp - (TpTicks * TickSize);
            else currentTP = currentIsLong ? refEntryForTp * (1 + (TpPercent / 100)) : refEntryForTp * (1 - (TpPercent / 100));

            bool placeTP = !(UseTrail && TrailRmLimit);
            bool gapExit = false;
            if (State == State.Realtime) realtimeTradeSeen = true;

            bool laterPiece = entryStopSent && stopOrder != null && !IsDone(stopOrder.OrderState);
            if (closingTrade)
            {
                laterPiece = true;
            }
            else if (laterPiece)
            {
                stopSyncPending = true;
                if (placeTP && currentTP > 0) tpSyncPending = true;
            }
            else if (marketPosition == MarketPosition.Long)
            {
                if (SafeBid() <= currentSL)
                {
                    gapExit = true;
                    closingTrade = true; closeName = "Stop Gap Exit"; lastCloseSent = DateTime.Now;
                    Order g = ExitLong("Stop Gap Exit", "ORB_Entry");
                    if (g != null) closeOrder = g;
                }
                else
                {
                    lastStopSubmit = DateTime.Now;
                    Order so = ExitLongStopMarket(0, true, entryFilledQty, currentSL, "ORB_SL", "ORB_Entry");
                    if (so != null) stopOrder = so;
                    if (placeTP && currentTP > 0) { Order to = ExitLongLimit(0, true, entryFilledQty, currentTP, "ORB_TP", "ORB_Entry"); if (to != null) tpOrder = to; }
                }
            }
            else if (marketPosition == MarketPosition.Short)
            {
                if (SafeAsk() >= currentSL)
                {
                    gapExit = true;
                    closingTrade = true; closeName = "Stop Gap Exit"; lastCloseSent = DateTime.Now;
                    Order g = ExitShort("Stop Gap Exit", "ORB_Entry");
                    if (g != null) closeOrder = g;
                }
                else
                {
                    lastStopSubmit = DateTime.Now;
                    Order so = ExitShortStopMarket(0, true, entryFilledQty, currentSL, "ORB_SL", "ORB_Entry");
                    if (so != null) stopOrder = so;
                    if (placeTP && currentTP > 0) { Order to = ExitShortLimit(0, true, entryFilledQty, currentTP, "ORB_TP", "ORB_Entry"); if (to != null) tpOrder = to; }
                }
            }

            if (gapExit && LogTiming)
                OrbLog(string.Format("[ORB] GAP EXIT {0:HH:mm:ss.fff} PC | price already through stop {1} (bid {2} / ask {3})", DateTime.Now, currentSL, SafeBid(), SafeAsk()));

            if (!laterPiece)
            {
                stopPlaced = !gapExit;
                if (!gapExit) entryStopSent = true;
                backupSL = currentSL;
            }
            needLineRedraw = true;

            if (LogTiming)
            {
                double slipTicks = (marketPosition == MarketPosition.Long) ? (price - plannedEntry) / TickSize : (plannedEntry - price) / TickSize;
                OrbLog(string.Format("[ORB] FILL  {0:HH:mm:ss.fff} PC | {1} x{2} @ {3} | signal close {4} | adverse slippage {5:0.##} ticks | avg entry {6} | initial SL {7} (rebase {8})",
                    DateTime.Now, marketPosition, quantity, price, plannedEntry, slipTicks, currentEntry, currentSL, RebaseToFill ? "ON" : "OFF"));
            }
        }

        private string CloseToNowMs(DateTime barCloseChartTime)
        {
            try
            {
                TimeZoneInfo src = sourceZoneCached ?? ResolveSourceZone();
                DateTime closeLocal = TimeZoneInfo.ConvertTime(barCloseChartTime, src, TimeZoneInfo.Local);
                return ((int)(DateTime.Now - closeLocal).TotalMilliseconds).ToString();
            }
            catch { return "n/a"; }
        }

        private static bool IsDone(OrderState st)
        {
            return st == OrderState.Filled || st == OrderState.Cancelled || st == OrderState.Rejected || st == OrderState.Unknown;
        }

        private int StopQty()
        {
            if (stopOrder != null && !IsDone(stopOrder.OrderState) && stopOrder.Filled > 0) return stopOrder.Quantity;
            return Position.Quantity;
        }

        private static bool SameOrder(Order a, Order b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            return !string.IsNullOrEmpty(a.OrderId) && a.OrderId == b.OrderId;
        }

        private double BrokerStopPrice()
        {
            if ((LocalTrail || TvEngine) && !double.IsNaN(backupSL)) return backupSL;
            return currentSL;
        }

        private void PlaceStop(int qty, double price)
        {
            if (stopOrder == null || IsDone(stopOrder.OrderState)) return;
            lastStopSubmit = DateTime.Now;
            Order o = (Position.MarketPosition == MarketPosition.Long)
                ? ExitLongStopMarket(0, true, qty, price, "ORB_SL", "ORB_Entry")
                : ExitShortStopMarket(0, true, qty, price, "ORB_SL", "ORB_Entry");
            if (o != null) stopOrder = o;
        }

        private void SyncStop(double wanted)
        {
            if (stopOrder == null || IsDone(stopOrder.OrderState)) return;
            OrderState st = stopOrder.OrderState;
            bool ready = st == OrderState.Working || st == OrderState.Accepted || st == OrderState.TriggerPending || st == OrderState.PartFilled;
            if (!ready) return;
            if ((DateTime.Now - lastStopChangeError).TotalMilliseconds < 500) return;
            if (double.IsNaN(wanted)) return;

            double target = wanted;
            if (Position.MarketPosition == MarketPosition.Long)
            {
                target = Math.Min(target, Instrument.MasterInstrument.RoundToTickSize(SafeBid() - StopSafetyTicks * TickSize));
                if (target <= stopOrder.StopPrice) return;
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                target = Math.Max(target, Instrument.MasterInstrument.RoundToTickSize(SafeAsk() + StopSafetyTicks * TickSize));
                if (target >= stopOrder.StopPrice) return;
            }
            else return;

            PlaceStop(StopQty(), target);
        }

        private bool SendClose(string name)
        {
            if (Position.MarketPosition == MarketPosition.Flat) return false;

            bool first = !closingTrade;
            closingTrade = true;
            closeName = name;

            bool stopAlive = stopOrder != null && !IsDone(stopOrder.OrderState);
            bool tpAlive = tpOrder != null && !IsDone(tpOrder.OrderState);
            if (stopAlive || tpAlive)
            {
                if (!exitCancelSent || (DateTime.Now - exitCancelSentAt).TotalMilliseconds > 1500)
                {
                    exitCancelSent = true;
                    exitCancelSentAt = DateTime.Now;
                    if (stopAlive && stopOrder != null) CancelOrder(stopOrder);
                    if (tpAlive && tpOrder != null && !IsDone(tpOrder.OrderState)) CancelOrder(tpOrder);
                }
                return first;
            }

            if ((DateTime.Now - restingExitFilledAt).TotalMilliseconds < 2000) return false;
            if (ExitFillInProgress()) return false;

            if (closeOrder != null && !IsDone(closeOrder.OrderState)) return false;
            if (closeOrder != null && closeOrder.OrderState == OrderState.Filled && (DateTime.Now - lastCloseSent).TotalSeconds < 3) return false;
            if (lastCloseSent != DateTime.MinValue && (DateTime.Now - lastCloseSent).TotalMilliseconds < 500) return false;

            lastCloseSent = DateTime.Now;
            int qty = Position.Quantity;
            Order o = (Position.MarketPosition == MarketPosition.Long)
                ? ExitLong(0, qty, name, "ORB_Entry")
                : ExitShort(0, qty, name, "ORB_Entry");
            if (o != null) closeOrder = o;
            return true;
        }

        private void CheckLocalTrailExit()
        {
            if (emergencyFlattenPending || closingTrade) return;

            bool isLong = Position.MarketPosition == MarketPosition.Long;
            bool isShort = Position.MarketPosition == MarketPosition.Short;

            double checkPrice = Close[0];
            if (State == State.Realtime)
            {
                if (TrailTrigger == TrailTriggerType.LastTradeAndBidAsk)
                {
                    checkPrice = isLong ? Math.Max(Close[0], SafeBid()) : Math.Min(Close[0], SafeAsk());
                }
                else if (TrailTrigger == TrailTriggerType.LastTradeOrBidAsk)
                {
                    checkPrice = isLong ? Math.Min(Close[0], SafeBid()) : Math.Max(Close[0], SafeAsk());
                }
            }

            bool hit = false;
            double breachTicks = 0;
            if (isLong)
            {
                hit = checkPrice <= currentSL;
                if (hit) breachTicks = (currentSL - checkPrice) / TickSize;
            }
            else if (isShort)
            {
                hit = checkPrice >= currentSL;
                if (hit) breachTicks = (checkPrice - currentSL) / TickSize;
            }

            if (!hit)
            {
                trailBreachStart = DateTime.MinValue;
                return;
            }

            if (State == State.Realtime && (TrailActivationGraceSec > 0 || TrailConfirmMs > 0))
            {
                if (TrailActivationGraceSec > 0 && trailActivatedAt != DateTime.MinValue
                    && (DateTime.Now - trailActivatedAt).TotalSeconds < TrailActivationGraceSec)
                {
                    return;
                }

                if (TrailConfirmMs > 0)
                {
                    if (trailBreachStart == DateTime.MinValue)
                    {
                        trailBreachStart = DateTime.Now;
                        return;
                    }
                    if ((DateTime.Now - trailBreachStart).TotalMilliseconds < TrailConfirmMs)
                    {
                        return;
                    }
                }
            }

            if (SendClose("ORB_Trail") && LogTiming)
                OrbLog(string.Format("[ORB] TRAIL HIT {0:HH:mm:ss.fff} PC | trail level {1} | last {2} (breach {3:0.#} ticks) | bid {4} / ask {5} | backup stop cancelled first, then market exit",
                    DateTime.Now, currentSL, Close[0], breachTicks, SafeBid(), SafeAsk()));
        }

        private int TvIntrabarSeconds()
        {
            if (BarsArray != null && BarsArray.Length > 0 && BarsArray[0] != null && BarsArray[0].BarsPeriod.BarsPeriodType != Data.BarsPeriodType.Minute) return 0;
            int m = chartMinutes > 0 ? chartMinutes : BarsPeriod.Value;
            if (TvBarDetail == TvBarDetailType.Default) return m * 60;
            if (TvBarDetail == TvBarDetailType.DeepBacktest1Min) return m <= 1 ? 10 : 60;
            if (m < 5) return 10;
            if (m < 10) return 30;
            if (m < 15) return 60;
            if (m < 30) return 120;
            if (m < 60) return 300;
            if (m < 240) return 600;
            if (m < 1440) return 1800;
            return 3600;
        }

        private bool ExitFillInProgress()
        {
            return lastExitExecAt != DateTime.MinValue && (DateTime.Now - lastExitExecAt).TotalMilliseconds < 1500;
        }

        private static string TvReasonText(int reason)
        {
            switch (reason)
            {
                case 1: return "trail";
                case 2: return "stop loss";
                case 3: return "take profit";
                default: return "-";
            }
        }

        private double TvTargetPrice()
        {
            if (UseTrail && TrailRmLimit) return double.NaN;
            double e = TvRefEntry();
            double r = plannedSlSize > 0 ? plannedSlSize : TrailRefSlSize();
            if (double.IsNaN(e)) return double.NaN;
            if (TakeProfitMode == TPModeType.RiskReward) return currentIsLong ? e + r * RrRatio : e - r * RrRatio;
            if (TakeProfitMode == TPModeType.FixedPointsTicks) return currentIsLong ? e + TpTicks * TickSize : e - TpTicks * TickSize;
            return currentIsLong ? e * (1 + TpPercent / 100) : e * (1 - TpPercent / 100);
        }

        private void TvSimReset(DateTime signalBarClose)
        {
            double refEntry = TvRefEntry();
            tvSimActive = true;
            tvSimActivated = false;
            tvSimExited = false;
            tvShadow = false;
            tvSimBest = refEntry;
            tvSimLastPx = refEntry;
            tvLastExitPx = double.NaN;
            tvExitReason = 0;
            tvExitDesc = "";
            tvSimLastBarTime = signalBarClose;
            tvSimStart = signalBarClose;
            // AfterEntryOnly: the sub-bar that crosses the entry candle's border starts fresh at the entry
            if (TvStraddle == TvStraddleType.AfterEntryOnly) tvPendHas = false;
        }

        // true while bars arrive tick by tick (live, playback, tick replay). false = each call is a finished bar (plain history / Strategy Analyzer).
        private bool TvPerTick()
        {
            if (State == State.Realtime) return true;
            try { return BarsArray[0].IsTickReplay; } catch { return false; }
        }

        // minutes since the CME session open (18:00 New York). TradingView's sub-bars are anchored there, so 2-min sub-bars sit on even minutes.
        private long TvMinuteIndex(DateTime chartTime)
        {
            return (long)Math.Floor((GetNYTime(chartTime) - tvAnchor).TotalMinutes + 1e-6);
        }

        // Called for every bar of the tvBip series once it is complete. Returns true when a new bar was taken.
        private bool TvProcessBase(int barsAgo)
        {
            if (tvBip <= 0 || CurrentBars[tvBip] < barsAgo) return false;
            int abs = CurrentBars[tvBip] - barsAgo;
            if (abs <= tvLastBaseIdx) return false;
            tvLastBaseIdx = abs;

            DateTime close = Times[tvBip][barsAgo];
            DateTime open = close.AddSeconds(-tvBaseSec);
            double o = Opens[tvBip][barsAgo], h = Highs[tvBip][barsAgo], l = Lows[tvBip][barsAgo], c = Closes[tvBip][barsAgo];

            if (tvAgg <= 1)
            {
                TvFeedSubBar(open, close, o, h, l, c, double.NaN);
                return true;
            }

            if (TvStraddle == TvStraddleType.AfterEntryOnly && tvSimActive && close <= tvSimStart) return true;

            long m = TvMinuteIndex(open);
            long idx = (long)Math.Floor((double)m / tvAgg);
            int pos = (int)(m - idx * tvAgg);
            if (tvPendHas && tvPendIdx != idx) TvFlushPending();
            if (!tvPendHas)
            {
                tvPendHas = true;
                tvPendIdx = idx;
                tvPendOpenTime = open.AddMinutes(-pos);
                tvPO = o; tvPH = h; tvPL = l; tvPC = c;
                tvPendChartOpen = double.NaN;
            }
            else
            {
                if (h > tvPH) tvPH = h;
                if (l < tvPL) tvPL = l;
                tvPC = c;
            }
            // this minute opens a new chart candle in the middle of a sub-bar -> remember the candle open (TradingView prints it first)
            if (pos > 0 && chartMinutes > 0 && m % chartMinutes == 0) tvPendChartOpen = o;
            if (pos == tvAgg - 1) TvFlushPending();
            return true;
        }

        private void TvFlushPending()
        {
            if (!tvPendHas) return;
            tvPendHas = false;
            TvFeedSubBar(tvPendOpenTime, tvPendOpenTime.AddSeconds(tvAgg * tvBaseSec), tvPO, tvPH, tvPL, tvPC, tvPendChartOpen);
        }

        // One finished TradingView sub-bar -> walk TradingView's price path through it.
        private void TvFeedSubBar(DateTime open, DateTime close, double o, double h, double l, double c, double chartOpenPx)
        {
            if (!tvSimActive || tvSimExited) return;
            if (TvStraddle == TvStraddleType.Skip ? open < tvSimStart : close <= tvSimStart) return;
            string len = tvSubSec >= 60 ? (tvSubSec / 60) + "-min" : tvSubSec + "-sec";
            if (TvStraddle == TvStraddleType.FullSubBar && !double.IsNaN(chartOpenPx))
            {
                TvSimWalk(chartOpenPx, chartOpenPx, chartOpenPx, chartOpenPx, string.Format("candle open print before the {0:HH:mm:ss}-{1:HH:mm:ss} sub-bar", open, close));
                if (!tvSimActive) return;
            }
            TvSimWalk(o, h, l, c, string.Format("{0} sub-bar {1:HH:mm:ss}-{2:HH:mm:ss}", len, open, close));
        }

        // A chart candle just closed (timer close, or first tick of the next candle).
        private void TvChartBarClosed(DateTime chartClose, double chartClosePx)
        {
            if (tvBip <= 0 || chartClose == tvChartCloseDone) return;
            tvChartCloseDone = chartClose;

            // 1) the last sub-bar data of the candle is complete now, even though its own "next tick" has not arrived yet
            if (TvPerTick() && CurrentBars[tvBip] >= 0 && Times[tvBip][0] <= chartClose) TvProcessBase(0);

            // 2) TradingView prints the candle close when the candle's last minute belongs to the next candle's sub-bar
            if (TvStraddle == TvStraddleType.FullSubBar && tvAgg > 1 && tvSimActive && !tvSimExited && chartClose > tvSimStart
                && TvMinuteIndex(chartClose) % tvAgg != 0)
                TvSimWalk(chartClosePx, chartClosePx, chartClosePx, chartClosePx, string.Format("candle close print {0:HH:mm:ss}", chartClose));
        }

        // tvBip series update
        private void TvOnBaseSeries()
        {
            bool took;
            if (TvPerTick()) took = IsFirstTickOfBar && CurrentBar > 0 && TvProcessBase(1);
            else took = TvProcessBase(0);
            if (took && State == State.Realtime) TvActOnSim();
        }

        // Act on the live trade right after a sub-bar finished (does not wait for the next chart tick).
        private void TvActOnSim()
        {
            if (!TvEngine || State != State.Realtime) return;
            if (Position.MarketPosition == MarketPosition.Flat || closingTrade || !stopPlaced || WrongSide()) return;
            if (tvSimExited) TvSendExit();
            else if (tvSimActive)
            {
                bool moved = false;
                TvEngineManage(false, ref moved);
            }
        }

        private void TvSimWalk(double o, double h, double l, double c, string barDesc)
        {
            if (!tvSimActive || tvSimExited) return;
            bool isLong = currentIsLong;
            double tick = TickSize;
            double refEntry = TvRefEntry();
            if (double.IsNaN(refEntry)) return;
            double actRef = TrailActRefEntry();
            if (double.IsNaN(actRef) || actRef <= 0) actRef = refEntry;
            double actPx = isLong ? actRef + TrailActTicks() * tick : actRef - TrailActTicks() * tick;
            double offPts = TrailOffTicks() * tick;
            double slPx = !double.IsNaN(plannedSL) ? plannedSL : initialSL;
            double tpPx = TvTargetPrice();
            bool act = tvSimActivated;
            double best = double.IsNaN(tvSimBest) ? refEntry : tvSimBest;
            double prev = double.IsNaN(tvSimLastPx) ? refEntry : tvSimLastPx;
            double exitPx;
            int reason = TvWalkBar(isLong, prev, o, h, l, c, slPx, tpPx, actPx, offPts, true, ref act, ref best, out exitPx);
            tvSimLastPx = c;
            if (reason == 0)
            {
                if (LogTiming && act && (!tvSimActivated || Math.Abs(best - tvSimBest) > tick * 0.1))
                    OrbLog(string.Format("[ORB] TV TRAIL {0} | {1} (O {2} H {3} L {4} C {5}) | best {6} | TradingView trail level {7}",
                        tvSimActivated ? "MOVED" : "ACTIVE", barDesc, o, h, l, c, best, isLong ? best - offPts : best + offPts));
                tvSimActivated = act;
                tvSimBest = best;
                return;
            }

            tvSimActivated = act;
            tvSimBest = best;
            tvSimExited = true;
            tvSimActive = false;
            tvLastExitPx = exitPx;
            tvExitReason = reason;
            tvExitDesc = barDesc;
            bool wasShadow = tvShadow;
            tvShadow = false;
            if (LogTiming)
                OrbLog(string.Format("[ORB] TV EXIT | TradingView exit: {0} at {1} | in the {2} (O {3} H {4} L {5} C {6}) | entry {7} | best {8} | result {9:0.##} pts | {10}",
                    TvReasonText(reason).ToUpper(), exitPx, barDesc, o, h, l, c, refEntry, best,
                    isLong ? exitPx - refEntry : refEntry - exitPx,
                    wasShadow ? "our stop had already closed the live trade -> new entries allowed again"
                        : (Position.MarketPosition != MarketPosition.Flat ? "live trade is closed now at market" : "no open position")));
        }

        // TradingView's trade ended inside the sub-bar that just finished -> close the live trade now.
        private void TvSendExit()
        {
            if (closingTrade || Position.MarketPosition == MarketPosition.Flat) return;
            string name = tvExitReason == 2 ? "ORB_TV_SL" : (tvExitReason == 3 ? "ORB_TV_TP" : "ORB_TV_Trail");
            double px = Close[0];
            if (SendClose(name) && LogTiming)
                OrbLog(string.Format("[ORB] TV EXIT SENT {0:HH:mm:ss.fff} PC | {1} | TradingView exit price {2} | market now {3} ({4:+0.#;-0.#;0} ticks vs TradingView) | stop cancelled first, then market exit",
                    DateTime.Now, name, tvLastExitPx, px,
                    double.IsNaN(tvLastExitPx) ? 0 : (currentIsLong ? (px - tvLastExitPx) : (tvLastExitPx - px)) / TickSize));
        }

        private void TvEndShadow(string why)
        {
            if (tvShadow && LogTiming)
                OrbLog(string.Format("[ORB] TV SHADOW OFF {0:HH:mm:ss.fff} PC | {1} -> new entries allowed again", DateTime.Now, why));
            tvShadow = false;
            tvSimActive = false;
        }

        // v3.2: the broker stop sits exactly where TradingView's trail stood at the end of the last finished sub-bar.
        // It is never moved inside an open sub-bar, because TradingView does not know the path inside it either.
        private void TvEngineManage(bool entrySyncSent, ref bool slMoved)
        {
            bool isLong = currentIsLong;
            if ((isLong && Position.MarketPosition != MarketPosition.Long) || (!isLong && Position.MarketPosition != MarketPosition.Short)) return;
            double tick = TickSize;

            // best live price is kept for the exit logs only
            if (isLong) highestPriceSinceEntry = Math.Max(highestPriceSinceEntry, Close[0]);
            else lowestPriceSinceEntry = Math.Min(lowestPriceSinceEntry, Close[0]);

            if (!tvSimActivated || double.IsNaN(tvSimBest)) return;

            double level = Instrument.MasterInstrument.RoundToTickSize(isLong ? tvSimBest - TrailOffTicks() * tick : tvSimBest + TrailOffTicks() * tick);
            if (!double.IsNaN(initialSL)) level = isLong ? Math.Max(level, initialSL) : Math.Min(level, initialSL);
            if (double.IsNaN(currentSL) || (isLong ? level > currentSL + tick * 0.1 : level < currentSL - tick * 0.1))
            {
                currentSL = level;
                slMoved = true;
                if (LogTiming)
                    OrbLog(string.Format("[ORB] TV LEVEL STOP {0:HH:mm:ss.fff} PC | finished sub-bar confirms TradingView's trail (best {1}) -> broker stop goes to {2}",
                        DateTime.Now, tvSimBest, currentSL));
            }
            backupSL = currentSL;
            brokerStopAtTvLevel = true;
            MarkTrailOn(tvSimBest);

            bool stopAlive = stopOrder != null && !IsDone(stopOrder.OrderState);
            bool stopAlreadyCompleted = entryStopSent && !stopAlive;
            bool stopFilling = closingTrade || stopAlreadyCompleted || (stopAlive && (stopOrder.Filled > 0 || stopOrder.OrderState == OrderState.PartFilled)) || ExitFillInProgress() || (restingExitFilledAt != DateTime.MinValue && (DateTime.Now - restingExitFilledAt).TotalMilliseconds < 3000);
            bool stopAtLevel = stopAlive && (isLong ? stopOrder.StopPrice >= backupSL - tick * 0.5 : stopOrder.StopPrice <= backupSL + tick * 0.5);
            double confirmPrice = Close[0];
            if (State == State.Realtime && TrailTrigger == TrailTriggerType.LastTradeAndBidAsk)
                confirmPrice = isLong ? Math.Max(Close[0], SafeBid()) : Math.Min(Close[0], SafeAsk());
            // the next sub-bar opened at or through the new level: TradingView exits there at the open -> market exit
            bool through = !stopFilling && stopAlive && !stopAtLevel && (isLong ? confirmPrice <= backupSL : confirmPrice >= backupSL);

            if (through)
            {
                string stopWasAt = stopAlive ? stopOrder.StopPrice.ToString() : "none";
                if (SendClose("ORB_TV_Trail") && LogTiming)
                    OrbLog(string.Format("[ORB] TV STOP HIT {0:HH:mm:ss.fff} PC | price {1} is already at TradingView's trail level {2} (broker stop was {3}) -> market exit",
                        DateTime.Now, Close[0], backupSL, stopWasAt));
            }
            else if (!stopFilling && !entrySyncSent && !stopSyncPending)
                SyncStop(backupSL);
        }

        public static int TvWalkBar(bool isLong, double prev, double o, double h, double l, double c,
            double slPx, double tpPx, double actPx, double offPts, bool trailOn, ref bool act, ref double best, out double exitPx)
        {
            exitPx = double.NaN;
            const double eps = 1e-9;
            bool highFirst = (h - o) < (o - l);
            double[] path = highFirst ? new double[] { o, h, l, c } : new double[] { o, l, h, c };
            for (int i = 0; i < path.Length; i++)
            {
                double p = path[i];
                bool atOpen = i == 0;
                if (isLong)
                {
                    if (p > prev + eps)
                    {
                        if (!double.IsNaN(tpPx) && p >= tpPx - eps) { exitPx = atOpen ? Math.Max(p, tpPx) : tpPx; return 3; }
                        if (p > best) best = p;
                        if (trailOn && !act && best >= actPx - eps) act = true;
                    }
                    else if (p < prev - eps)
                    {
                        double stop = double.IsNaN(slPx) ? double.MinValue : slPx;
                        int why = 2;
                        if (trailOn && act && best - offPts > stop) { stop = best - offPts; why = 1; }
                        if (p <= stop + eps) { exitPx = atOpen ? Math.Min(p, stop) : stop; return why; }
                    }
                }
                else
                {
                    if (p < prev - eps)
                    {
                        if (!double.IsNaN(tpPx) && p <= tpPx + eps) { exitPx = atOpen ? Math.Min(p, tpPx) : tpPx; return 3; }
                        if (p < best) best = p;
                        if (trailOn && !act && best <= actPx + eps) act = true;
                    }
                    else if (p > prev + eps)
                    {
                        double stop = double.IsNaN(slPx) ? double.MaxValue : slPx;
                        int why = 2;
                        if (trailOn && act && best + offPts < stop) { stop = best + offPts; why = 1; }
                        if (p >= stop - eps) { exitPx = atOpen ? Math.Max(p, stop) : stop; return why; }
                    }
                }
                prev = p;
            }
            return 0;
        }

        private bool WrongSide()
        {
            if (State != State.Realtime || !realtimeTradeSeen) return false;
            if (Position.MarketPosition == MarketPosition.Long) return !currentIsLong;
            if (Position.MarketPosition == MarketPosition.Short) return currentIsLong;
            return false;
        }

        private void FixWrongSide()
        {
            if (strayAttempts >= 3) return;
            if (lastStrayAttempt != DateTime.MinValue && (DateTime.Now - lastStrayAttempt).TotalMilliseconds < 1000) return;
            lastStrayAttempt = DateTime.Now;
            strayAttempts++;
            tradingHalted = true;

            MarketPosition mp = Position.MarketPosition;
            int qty = Position.Quantity;
            try { Account.Flatten(new[] { Instrument }); } catch { }

            OrbLog(string.Format("[ORB] REVERSE GUARD {0:HH:mm:ss.fff} PC | unexpected {1} x{2} | account flattened (attempt {3}) | NEW ENTRIES HALTED - check account, then restart strategy",
                DateTime.Now, mp, qty, strayAttempts));
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            if (marketPosition == MarketPosition.Flat)
            {
                strayAttempts = 0;
                closingTrade = false;
                exitCancelSent = false;
                closeOrder = null;
                if (tvSimActive)
                {
                    bool hardStopExit = !tvSimExited && trailActivated && lastExitName == "ORB_SL";
                    if (TvEngine && TvShadowBlock && hardStopExit && !pendingT2Reversion)
                    {
                        if (!tvShadow && LogTiming)
                            OrbLog(string.Format("[ORB] TV SHADOW ON {0:HH:mm:ss.fff} PC | our stop at TradingView's trail level {1} closed the trade; TradingView confirms its own exit when the current sub-bar finishes -> no new entry until then", DateTime.Now, currentSL));
                        tvShadow = true;
                    }
                    else
                    {
                        tvSimActive = false;
                        tvShadow = false;
                    }
                }
                if (pendingT2Reversion)
                {
                    bool validWindow = pendingT2QueuedAt != DateTime.MinValue && (DateTime.Now - pendingT2QueuedAt).TotalSeconds < 15;
                    bool revLong = pendingT2IsLong;
                    double revHi = pendingT2OrbHigh;
                    double revLo = pendingT2OrbLow;
                    int revOff = pendingT2Offset;
                    pendingT2Reversion = false;
                    pendingT2QueuedAt = DateTime.MinValue;
                    if (validWindow && !tradingHalted && breakoutCount < EffMaxBreakouts())
                    {
                        if (LogTiming)
                            OrbLog(string.Format("[ORB] T2 REVERSION EXECUTING {0:HH:mm:ss.fff} PC | T1 flattened -> entering queued T2 {1}", DateTime.Now, revLong ? "LONG" : "SHORT"));
                        TriggerEntry(revLong, revOff, revHi, revLo);
                    }
                }
                return;
            }
            if (WrongSide()) FixWrongSide();
        }

        private static bool IsReady(OrderState st)
        {
            return st == OrderState.Working || st == OrderState.Accepted || st == OrderState.TriggerPending;
        }

        private bool SyncEntryProtection()
        {
            bool sentStop = false;
            bool isLong = Position.MarketPosition == MarketPosition.Long;
            bool isShort = Position.MarketPosition == MarketPosition.Short;
            if (!isLong && !isShort) { stopSyncPending = false; tpSyncPending = false; return false; }

            if (stopSyncPending)
            {
                if (stopOrder == null || IsDone(stopOrder.OrderState) || stopOrder.Filled > 0)
                    stopSyncPending = false;
                else if (IsReady(stopOrder.OrderState))
                {
                    double price = BrokerStopPrice();
                    if (isLong) price = Math.Min(price, Instrument.MasterInstrument.RoundToTickSize(SafeBid() - StopSafetyTicks * TickSize));
                    else price = Math.Max(price, Instrument.MasterInstrument.RoundToTickSize(SafeAsk() + StopSafetyTicks * TickSize));
                    if (stopOrder.Quantity != entryFilledQty || Math.Abs(stopOrder.StopPrice - price) > TickSize / 2)
                    {
                        PlaceStop(entryFilledQty, price);
                        sentStop = true;
                    }
                    stopSyncPending = false;
                }
            }

            if (tpSyncPending)
            {
                if (tpOrder == null || IsDone(tpOrder.OrderState) || tpOrder.Filled > 0)
                    tpSyncPending = false;
                else if (IsReady(tpOrder.OrderState))
                {
                    if (tpOrder.Quantity != entryFilledQty || Math.Abs(tpOrder.LimitPrice - currentTP) > TickSize / 2)
                    {
                        Order to = isLong ? ExitLongLimit(0, true, entryFilledQty, currentTP, "ORB_TP", "ORB_Entry")
                                          : ExitShortLimit(0, true, entryFilledQty, currentTP, "ORB_TP", "ORB_Entry");
                        if (to != null) tpOrder = to;
                    }
                    tpSyncPending = false;
                }
            }
            return sentStop;
        }

        private double SafeBid()
        {
            double b = (State == State.Realtime) ? GetCurrentBid() : 0;
            return b > 0 ? b : Close[0];
        }

        private double SafeAsk()
        {
            double a = (State == State.Realtime) ? GetCurrentAsk() : 0;
            return a > 0 ? a : Close[0];
        }

        private static readonly object orbLogLock = new object();
        private void OrbLog(string msg)
        {
            bool live = State == State.Realtime || msg.StartsWith("[ORB] SETTINGS");
            string line = (live ? "" : "(history replay) ") + msg;
            Print(line);
            if (!live) return;
            string stamped = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + line + Environment.NewLine;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", "ORB_Logs");
                    System.IO.Directory.CreateDirectory(dir);
                    string file = System.IO.Path.Combine(dir, "ORB_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
                    lock (orbLogLock) { System.IO.File.AppendAllText(file, stamped); }
                }
                catch { }
            });
        }

        private void MarkTrailOn(double extreme)
        {
            if (trailActivated) return;
            trailActivated = true;
            trailActivatedAt = DateTime.Now;
            trailBreachStart = DateTime.MinValue;
            if (LogTiming)
                OrbLog(string.Format("[ORB] TRAIL ON {0:HH:mm:ss.fff} PC | best price {1} | ref entry {2} (fill {3}) | R dist {4:0.##} pts | activation {5}R ({6} ticks) | behind {7}R ({8} ticks) | currentSL {9}",
                    DateTime.Now, extreme, TrailRefEntry(), currentEntry, TrailRefSlSize(), TrailActR, TrailActTicks(), TrailOffsetR, TrailOffTicks(), currentSL));
        }

        private int NyMinutesNow()
        {
            DateTime ny;
            if (State == State.Realtime)
                ny = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local, nyZone);
            else
                ny = GetNYTime(Time[0].AddMinutes(-BarsPeriod.Value));
            return ny.Hour * 60 + ny.Minute;
        }

        private void HandleOrderError(Order order, OrderState orderState, ErrorCode error, string nativeError)
        {
            bool inPosition = Position.MarketPosition != MarketPosition.Flat;
            bool isStop = order.Name == "ORB_SL";
            bool isTp = order.Name == "ORB_TP";
            bool isEntry = order.Name == "ORB_Entry";

            OrbLog(string.Format("[ORB] ORDER ERROR {0:HH:mm:ss.fff} PC | order {1} | state {2} | error {3} | broker: {4}",
                DateTime.Now, order.Name, orderState, error, nativeError));

            if (error == ErrorCode.UnableToChangeOrder && isStop)
                lastStopChangeError = DateTime.Now;

            if (isStop && orderState == OrderState.Rejected && inPosition)
            {
                emergencyFlattenPending = true;
                FlattenNow("Stop Rejected Close");
                return;
            }

            if (!isStop && !isTp && !isEntry && orderState == OrderState.Rejected && inPosition && (stopOrder == null || IsDone(stopOrder.OrderState)))
            {
                emergencyFlattenPending = true;
            }
        }

        private void FlattenNow(string signalName)
        {
            lastEmergencyFlatten = DateTime.Now;
            SendClose(signalName);
        }

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            if (order == null) return;
            if (error != ErrorCode.NoError) HandleOrderError(order, orderState, error, nativeError);

            if (order.Name == "ORB_SL" || order.Name == "ORB_TP")
            {
                bool done = IsDone(orderState);
                if (order.Name == "ORB_SL")
                {
                    if (!done) stopOrder = order;
                    else if (stopOrder == null || SameOrder(stopOrder, order)) stopOrder = null;
                }
                else
                {
                    if (!done) tpOrder = order;
                    else if (tpOrder == null || SameOrder(tpOrder, order)) tpOrder = null;
                }

                if (orderState == OrderState.Filled || orderState == OrderState.PartFilled)
                {
                    restingExitFilledAt = DateTime.Now;
                    if (orderState == OrderState.Filled)
                        closingTrade = true;
                }
                else if (orderState == OrderState.Cancelled && closingTrade && exitCancelSent)
                    SendClose(closeName);
                return;
            }
            if (order.Name != "ORB_Entry") return;
            entryOrder = order;
            if (orderState == OrderState.Filled || orderState == OrderState.Cancelled || orderState == OrderState.Rejected)
            {
                if (orderState != OrderState.Filled && filled == 0 && LogTiming)
                    OrbLog(string.Format("[ORB] ENTRY MISSED {0:HH:mm:ss.fff} PC | limit {1} not filled", DateTime.Now, limitPrice));
                if (orderState != OrderState.Filled && filled == 0 && Position.MarketPosition == MarketPosition.Flat && tvSimActive)
                {
                    if (TvEngine && TvShadowBlock && !tvSimExited && !closingTrade)
                    {
                        tvShadow = true;
                        if (LogTiming)
                            OrbLog(string.Format("[ORB] TV SHADOW ON {0:HH:mm:ss.fff} PC | entry missed but TradingView filled at the candle close -> no new entry until TradingView's trade would close", DateTime.Now));
                    }
                    else
                    {
                        tvSimActive = false;
                        tvShadow = false;
                    }
                }
                entryOrder = null;
                entryLimitSentAt = DateTime.MinValue;
                entryAtClosePhase = false;
            }
        }

        private DateTime lastExceptionLog = DateTime.MinValue;

        protected override void OnBarUpdate()
        {
            try
            {
                OnBarUpdateCore();
            }
            catch (Exception ex)
            {
                if ((DateTime.Now - lastExceptionLog).TotalSeconds >= 5)
                {
                    lastExceptionLog = DateTime.Now;
                    OrbLog("[ORB] ERROR in OnBarUpdate on bar " + CurrentBar + ": " + ex.Message + " | " + ex.StackTrace);
                    try { Log("ORB strategy error: " + ex.Message + " (details in NinjaScript Output)", LogLevel.Error); } catch { }
                }
            }
        }

        private void OnBarUpdateCore()
        {
            if (tvBip > 0 && BarsInProgress == tvBip)
            {
                TvOnBaseSeries();
                return;
            }
            if (CurrentBar < BarsRequiredToTrade || BarsPeriod.Value > MaxTimeframeMins) return;

            if (BarsInProgress == 0)
            {
                if (entryTimer != null && State == State.Realtime) ArmEntryTimer();

                bool firstTick = IsFirstTickOfBar && CurrentBar > 0;

                if (postExitWatch)
                {
                    if (firstTick)
                    {
                        postExitWatch = false;
                        if (LogTiming)
                        {
                            double barHi = High[1];
                            double barLo = Low[1];
                            OrbLog(string.Format("[ORB] POST-EXIT bar closed {0:HH:mm} | high after exit {1} (bar high {2}) | low after exit {3} (bar low {4}) | last exit fill {5}",
                                Time[1], postExitHi, barHi, postExitLo, barLo, lastExitPrice));

                            if (LogTvShadow && !double.IsNaN(plannedEntry) && plannedEntry > 0)
                            {
                                double fullBarBest = currentIsLong ? Math.Max(lastExitBestPrice, barHi) : Math.Min(lastExitBestPrice, barLo);
                                double refEnt = TrailRefEntry();
                                double fullBarProfitTicks = (currentIsLong ? (fullBarBest - refEnt) : (refEnt - fullBarBest)) / TickSize;
                                double tvBarSL = lastExitSL;
                                if (fullBarProfitTicks >= TrailActTicks())
                                {
                                    double candidateSL = currentIsLong
                                        ? Instrument.MasterInstrument.RoundToTickSize(fullBarBest - TrailOffTicks() * TickSize)
                                        : Instrument.MasterInstrument.RoundToTickSize(fullBarBest + TrailOffTicks() * TickSize);
                                    if (currentIsLong && candidateSL > tvBarSL) tvBarSL = candidateSL;
                                    else if (!currentIsLong && candidateSL < tvBarSL) tvBarSL = candidateSL;
                                }
                                double tvOhlcIllusionTicks = (currentIsLong ? (tvBarSL - lastExitSL) : (lastExitSL - tvBarSL)) / TickSize;
                                if (tvOhlcIllusionTicks > 0.5)
                                {
                                    OrbLog(string.Format("[ORB] TV OHLC ILLUSION DETECTED on bar {0:HH:mm} | Price made a new extreme ({1}) AFTER your mid-bar trail exit ({2}). TradingView OHLC backtest assumes {1} happened BEFORE the pullback, inflating TV trail SL from {3} to {4} (+{5:0.#} ticks).",
                                        Time[1], fullBarBest, lastExitPrice, lastExitSL, tvBarSL, tvOhlcIllusionTicks));
                                }
                            }
                        }
                    }
                    else
                    {
                        postExitHi = Math.Max(postExitHi, Close[0]);
                        postExitLo = Math.Min(postExitLo, Close[0]);
                    }
                }

                if (firstTick && timerTradeBar >= 0)
                {
                    if (timerTradeBar == CurrentBar - 1) ReanchorToFinalBar();
                    else timerTradeBar = -1;
                }

                // bar that just closed: [1] while bars build tick by tick, [0] when each call is a finished bar (history)
                int closedBar = TvPerTick() ? 1 : 0;

                if (TvEngine)
                {
                    if (firstTick && CurrentBar >= closedBar)
                    {
                        if (tvBip > 0)
                            TvChartBarClosed(Time[closedBar], Close[closedBar]);
                        else if (tvSimActive && Time[closedBar] > tvSimLastBarTime)
                        {
                            tvSimLastBarTime = Time[closedBar];
                            TvSimWalk(Open[closedBar], High[closedBar], Low[closedBar], Close[closedBar], string.Format("{0:HH:mm}-{1:HH:mm} candle", Time[closedBar].AddMinutes(-BarsPeriod.Value), Time[closedBar]));
                        }
                    }
                    if (tvShadow && tvSimActive && Position.MarketPosition == MarketPosition.Flat && UseFlatten)
                    {
                        int nowNy = NyMinutesNow();
                        if (nowNy >= FlattenTime.Hour * 60 + FlattenTime.Minute && nowNy < 18 * 60)
                            TvEndShadow("end-of-day close (TradingView closes all trades too)");
                    }
                }

                bool wrongSide = WrongSide();
                if (wrongSide) FixWrongSide();
                if (Position.MarketPosition == MarketPosition.Flat) strayAttempts = 0;

                if (Position.MarketPosition == MarketPosition.Flat) emergencyFlattenPending = false;
                else if (!wrongSide && emergencyFlattenPending && (DateTime.Now - lastEmergencyFlatten).TotalSeconds >= 2)
                {
                    FlattenNow("Emergency Close Retry");
                }

                if (entryOrder != null && entryLimitSentAt != DateTime.MinValue && (DateTime.Now - entryLimitSentAt).TotalSeconds >= EntryLimitTimeoutSec
                    && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted || entryOrder.OrderState == OrderState.Submitted || entryOrder.OrderState == OrderState.PartFilled))
                {
                    entryLimitSentAt = DateTime.MinValue;
                    CancelOrder(entryOrder);
                }

                if (entryAtClosePhase)
                {
                    if (entryOrder == null || IsDone(entryOrder.OrderState) || closingTrade)
                        entryAtClosePhase = false;
                    else if (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted || entryOrder.OrderState == OrderState.PartFilled)
                    {
                        double waitedMs = (DateTime.Now - entryAtCloseSentAt).TotalMilliseconds;
                        double runTicks = !double.IsNaN(plannedEntry)
                            ? (currentIsLong ? (SafeAsk() - plannedEntry) / TickSize : (plannedEntry - SafeBid()) / TickSize) : 0;
                        bool ranAway = State == State.Realtime && ChaseRunTicks > 0 && runTicks + 1e-6 >= ChaseRunTicks;
                        if (waitedMs >= EntryAtCloseMs || ranAway)
                        {
                            entryAtClosePhase = false;
                            double chasePx = currentIsLong
                                ? Instrument.MasterInstrument.RoundToTickSize(SafeAsk() + EntryChaseTicks * TickSize)
                                : Instrument.MasterInstrument.RoundToTickSize(SafeBid() - EntryChaseTicks * TickSize);
                            int chaseQty = Math.Max(1, plannedQty);
                            if (currentIsLong) EnterLongLimit(0, true, chaseQty, chasePx, "ORB_Entry");
                            else EnterShortLimit(0, true, chaseQty, chasePx, "ORB_Entry");
                            if (LogTiming)
                                OrbLog(string.Format("[ORB] ENTRY CHASE {0:HH:mm:ss.fff} PC | not filled at signal close {1} {2} -> limit moved to {3} (bid {4} / ask {5}) x{6}",
                                    DateTime.Now, plannedEntry,
                                    waitedMs >= EntryAtCloseMs ? string.Format("after {0} ms", EntryAtCloseMs) : string.Format("and price already ran {0:0.#} ticks away after {1:0} ms (fast spike)", runTicks, waitedMs),
                                    chasePx, SafeBid(), SafeAsk(), chaseQty));
                        }
                    }
                }

                if (Position.MarketPosition != MarketPosition.Flat && !wrongSide)
                {
                    if (closingTrade)
                    {
                        SendClose(closeName);
                    }
                    else
                    {
                        bool entrySyncSent = SyncEntryProtection();

                        if (TvEngine && tvSimExited)
                            TvSendExit();
                        else if (UseTrail && stopPlaced)
                        {
                            bool slMoved = false;
                            if (TvEngine)
                            {
                                TvEngineManage(entrySyncSent, ref slMoved);
                            }
                            else
                            {
                            double actRefEntry = TrailActRefEntry();

                            if (Position.MarketPosition == MarketPosition.Long && currentIsLong)
                            {
                                double refHigh;
                                if (State == State.Historical)
                                    refHigh = High[0];
                                else if (FilterRatchetStrayTicks)
                                    refHigh = Math.Min(Close[0], SafeAsk());
                                else
                                    refHigh = Close[0];

                                highestPriceSinceEntry = Math.Max(highestPriceSinceEntry, refHigh);
                                double maxProfitTicks = (highestPriceSinceEntry - actRefEntry) / TickSize;

                                if (maxProfitTicks >= TrailActTicks())
                                {
                                    double newSL = Instrument.MasterInstrument.RoundToTickSize(highestPriceSinceEntry - (TrailOffTicks() * TickSize));
                                    if (newSL > currentSL) { currentSL = newSL; slMoved = true; }
                                    MarkTrailOn(highestPriceSinceEntry);
                                }
                            }
                            else if (Position.MarketPosition == MarketPosition.Short && !currentIsLong)
                            {
                                double refLow;
                                if (State == State.Historical)
                                    refLow = Low[0];
                                else if (FilterRatchetStrayTicks)
                                    refLow = Math.Max(Close[0], SafeBid());
                                else
                                    refLow = Close[0];

                                lowestPriceSinceEntry = Math.Min(lowestPriceSinceEntry, refLow);
                                double maxProfitTicks = (actRefEntry - lowestPriceSinceEntry) / TickSize;

                                if (maxProfitTicks >= TrailActTicks())
                                {
                                    double newSL = Instrument.MasterInstrument.RoundToTickSize(lowestPriceSinceEntry + (TrailOffTicks() * TickSize));
                                    if (newSL < currentSL) { currentSL = newSL; slMoved = true; }
                                    MarkTrailOn(lowestPriceSinceEntry);
                                }
                            }

                            if (LocalTrail)
                            {
                                if (trailActivated) CheckLocalTrailExit();

                                if (!closingTrade && trailActivated && BackupStopTicks > 0)
                                {
                                    double b = currentIsLong
                                        ? Instrument.MasterInstrument.RoundToTickSize(currentSL - BackupStopTicks * TickSize)
                                        : Instrument.MasterInstrument.RoundToTickSize(currentSL + BackupStopTicks * TickSize);
                                    if (double.IsNaN(backupSL) || (currentIsLong ? b > backupSL : b < backupSL)) backupSL = b;
                                    if (!entrySyncSent && !stopSyncPending) SyncStop(backupSL);
                                }
                            }
                            else
                            {
                                bool stopAlive = stopOrder != null && !IsDone(stopOrder.OrderState);
                                bool stopAlreadyCompleted = entryStopSent && !stopAlive;
                                bool stopFilling = closingTrade || stopAlreadyCompleted || (stopAlive && (stopOrder.Filled > 0 || stopOrder.OrderState == OrderState.PartFilled)) || ExitFillInProgress() || (restingExitFilledAt != DateTime.MinValue && (DateTime.Now - restingExitFilledAt).TotalMilliseconds < 3000);

                                if (!stopFilling && !entrySyncSent && !stopSyncPending)
                                {
                                    SyncStop(currentSL);
                                }

                                double graceDist = Math.Max(0.5, BrokerStopGraceTicks) * TickSize;
                                bool stopCloseEnough = stopAlive && (currentIsLong
                                    ? stopOrder.StopPrice >= currentSL - graceDist
                                    : stopOrder.StopPrice <= currentSL + graceDist);

                                bool modifyRecentlySent = lastStopSubmit != DateTime.MinValue
                                    && (DateTime.Now - lastStopSubmit).TotalMilliseconds < BrokerStopSyncWaitMs;

                                double confirmPrice = Close[0];
                                if (State == State.Realtime && TrailTrigger == TrailTriggerType.LastTradeAndBidAsk)
                                    confirmPrice = currentIsLong ? Math.Max(Close[0], SafeBid()) : Math.Min(Close[0], SafeAsk());

                                bool priceBreachedGrace = currentIsLong
                                    ? confirmPrice <= currentSL - (BrokerStopGraceTicks > 0 ? TickSize : 0)
                                    : confirmPrice >= currentSL + (BrokerStopGraceTicks > 0 ? TickSize : 0);

                                bool breachedBeforeSync = trailActivated && !stopFilling && !stopCloseEnough && !modifyRecentlySent && priceBreachedGrace;

                                if (breachedBeforeSync)
                                {
                                    string stopWasAt = stopAlive ? stopOrder.StopPrice.ToString() : "none";
                                    if (SendClose("ORB_Trail_Breach") && LogTiming)
                                        OrbLog(string.Format("[ORB] TRAIL BREACH {0:HH:mm:ss.fff} PC | price {1} breached trail {2} | broker stop was stuck at {3} (> {4}t behind) | stop cancelled first, then market exit",
                                            DateTime.Now, Close[0], currentSL, stopWasAt, BrokerStopGraceTicks));
                                }
                            }
                            } // end LiveTick branch

                            if (slMoved && ShowSlFill && liveTradeStartTime != DateTime.MinValue)
                                Draw.Rectangle(this, "LIVE_TRADE_SL_BOX", false, liveTradeStartTime, currentEntry, Time[0].AddMinutes(BarsPeriod.Value * 10), currentSL, Brushes.Transparent, Brushes.Red, 15);
                        }

                        if (!closingTrade && UseFlatten && Position.MarketPosition != MarketPosition.Flat)
                        {
                            int flatMins = FlattenTime.Hour * 60 + FlattenTime.Minute;
                            int nowMins = NyMinutesNow();
                            if (nowMins >= flatMins && nowMins < 18 * 60)
                            {
                                lastFlattenAttempt = DateTime.Now;
                                if (SendClose("EOD Flatten") && LogTiming)
                                    OrbLog(string.Format("[ORB] FLATTEN {0:HH:mm:ss.fff} PC | NY minutes {1} >= {2} | stop cancelled first, then market exit", DateTime.Now, nowMins, flatMins));
                            }
                        }
                    }

                    if (needLineRedraw)
                    {
                        needLineRedraw = false;
                        if (liveTradeStartTime != DateTime.MinValue)
                        {
                            DateTime redrawEnd = Time[0].AddMinutes(BarsPeriod.Value * MaxTradeBars);
                            if (ShowTradeLines)
                            {
                                Draw.Line(this, "LIVE_TRADE_ENTRY", false, liveTradeStartTime, currentEntry, redrawEnd, currentEntry, Brushes.Black, DashStyleHelper.Solid, 1);
                                Draw.Line(this, "LIVE_TRADE_SL", false, liveTradeStartTime, initialSL, redrawEnd, initialSL, Brushes.Red, DashStyleHelper.Dash, 1);
                                Draw.Line(this, "LIVE_TRADE_TP", false, liveTradeStartTime, currentTP, redrawEnd, currentTP, Brushes.Blue, DashStyleHelper.Solid, 2);
                            }
                            if (ShowSlFill) Draw.Rectangle(this, "LIVE_TRADE_SL_BOX", false, liveTradeStartTime, currentEntry, redrawEnd, initialSL, Brushes.Transparent, Brushes.Red, 15);
                            if (ShowTpFill) Draw.Rectangle(this, "LIVE_TRADE_TP_BOX", false, liveTradeStartTime, currentEntry, redrawEnd, currentTP, Brushes.Transparent, Brushes.Blue, 15);
                        }
                    }
                }

                if (firstTick && CurrentBar > closedBar)
                    EvaluateBarCloseLogic(closedBar, true);
            }
            else if (orbBip > 0 && BarsInProgress == orbBip)
            {
                if (IsFirstTickOfBar && CurrentBar > 0) EvaluateMtfLogic(1);
            }
        }

        private void EvaluateMtfLogic(int offset)
        {
            DateTime barOpenTime = Times[1][offset].AddMinutes(-OrbTfLockMinutes);
            DateTime nyOpenTime = GetNYTime(barOpenTime);
            int barMins = nyOpenTime.Hour * 60 + nyOpenTime.Minute;

            int aStart = AsiaSessionStart.Hour * 60 + AsiaSessionStart.Minute; int aEnd = AsiaSessionEnd.Hour * 60 + AsiaSessionEnd.Minute;
            int lStart = LondonSessionStart.Hour * 60 + LondonSessionStart.Minute; int lEnd = LondonSessionEnd.Hour * 60 + LondonSessionEnd.Minute;
            int nStart = NySessionStart.Hour * 60 + NySessionStart.Minute; int nEnd = NySessionEnd.Hour * 60 + NySessionEnd.Minute;
            int eStart = ExtraSessionStart.Hour * 60 + ExtraSessionStart.Minute; int eEnd = ExtraSessionEnd.Hour * 60 + ExtraSessionEnd.Minute;
            int s5Start = Slot5SessionStart.Hour * 60 + Slot5SessionStart.Minute; int s5End = Slot5SessionEnd.Hour * 60 + Slot5SessionEnd.Minute;
            int s6Start = Slot6SessionStart.Hour * 60 + Slot6SessionStart.Minute; int s6End = Slot6SessionEnd.Hour * 60 + Slot6SessionEnd.Minute;

            bool inAsia = IsInSessionStrict(barMins, aStart, aEnd);
            bool inLondon = IsInSessionStrict(barMins, lStart, lEnd);
            bool inNy = IsInSessionStrict(barMins, nStart, nEnd);
            bool inExtra = IsInSessionStrict(barMins, eStart, eEnd);
            bool inS5 = IsInSessionStrict(barMins, s5Start, s5End);
            bool inS6 = IsInSessionStrict(barMins, s6Start, s6End);

            if (inAsia) { mtfAsiaHigh = double.IsNaN(mtfAsiaHigh) ? Highs[1][offset] : Math.Max(mtfAsiaHigh, Highs[1][offset]); mtfAsiaLow = double.IsNaN(mtfAsiaLow) ? Lows[1][offset] : Math.Min(mtfAsiaLow, Lows[1][offset]); }
            if (inLondon) { mtfLondonHigh = double.IsNaN(mtfLondonHigh) ? Highs[1][offset] : Math.Max(mtfLondonHigh, Highs[1][offset]); mtfLondonLow = double.IsNaN(mtfLondonLow) ? Lows[1][offset] : Math.Min(mtfLondonLow, Lows[1][offset]); }
            if (inNy) { mtfNyHigh = double.IsNaN(mtfNyHigh) ? Highs[1][offset] : Math.Max(mtfNyHigh, Highs[1][offset]); mtfNyLow = double.IsNaN(mtfNyLow) ? Lows[1][offset] : Math.Min(mtfNyLow, Lows[1][offset]); }
            if (inExtra) { mtfExtraHigh = double.IsNaN(mtfExtraHigh) ? Highs[1][offset] : Math.Max(mtfExtraHigh, Highs[1][offset]); mtfExtraLow = double.IsNaN(mtfExtraLow) ? Lows[1][offset] : Math.Min(mtfExtraLow, Lows[1][offset]); }
            if (inS5) { mtfS5High = double.IsNaN(mtfS5High) ? Highs[1][offset] : Math.Max(mtfS5High, Highs[1][offset]); mtfS5Low = double.IsNaN(mtfS5Low) ? Lows[1][offset] : Math.Min(mtfS5Low, Lows[1][offset]); }
            if (inS6) { mtfS6High = double.IsNaN(mtfS6High) ? Highs[1][offset] : Math.Max(mtfS6High, Highs[1][offset]); mtfS6Low = double.IsNaN(mtfS6Low) ? Lows[1][offset] : Math.Min(mtfS6Low, Lows[1][offset]); }

            wasMtfAsia = inAsia; wasMtfLondon = inLondon; wasMtfNy = inNy; wasMtfExtra = inExtra; wasMtfS5 = inS5; wasMtfS6 = inS6;
        }

        private void EvaluateBarCloseLogic(int offset, bool allowEntries)
        {
            if (restoreIsLongWhenFlat && Position.MarketPosition == MarketPosition.Flat && entryOrder == null)
            {
                restoreIsLongWhenFlat = false;
                currentIsLong = prevIsLong;
            }

            DateTime barCloseTime = Time[offset];
            DateTime barOpenTime = barCloseTime.AddMinutes(-BarsPeriod.Value);
            DateTime nyOpenTime = GetNYTime(barOpenTime);
            int barTimeMins = nyOpenTime.Hour * 60 + nyOpenTime.Minute;

            DateTime prevBarOpenTime = Time[offset + 1].AddMinutes(-BarsPeriod.Value);
            DateTime prevNyOpenTime = GetNYTime(prevBarOpenTime);
            int prevBarTimeMins = prevNyOpenTime.Hour * 60 + prevNyOpenTime.Minute;

            bool firstEvalOfThisBar = (barCloseTime != lastEvaluatedBarClose);
            if (firstEvalOfThisBar)
            {
                wasInOrbBeforeBar = wasInOrb;
                lastEvaluatedBarClose = barCloseTime;
            }
            bool prevWasInOrb = wasInOrbBeforeBar;

            int bufferMins = Math.Max(BarsPeriod.Value, 30);
            int aStart = AsiaSessionStart.Hour * 60 + AsiaSessionStart.Minute; int aEnd = AsiaSessionEnd.Hour * 60 + AsiaSessionEnd.Minute;
            int lStart = LondonSessionStart.Hour * 60 + LondonSessionStart.Minute; int lEnd = LondonSessionEnd.Hour * 60 + LondonSessionEnd.Minute;
            int nStart = NySessionStart.Hour * 60 + NySessionStart.Minute; int nEnd = NySessionEnd.Hour * 60 + NySessionEnd.Minute;
            int eStart = ExtraSessionStart.Hour * 60 + ExtraSessionStart.Minute; int eEnd = ExtraSessionEnd.Hour * 60 + ExtraSessionEnd.Minute;
            int s5Start = Slot5SessionStart.Hour * 60 + Slot5SessionStart.Minute; int s5End = Slot5SessionEnd.Hour * 60 + Slot5SessionEnd.Minute;
            int s6Start = Slot6SessionStart.Hour * 60 + Slot6SessionStart.Minute; int s6End = Slot6SessionEnd.Hour * 60 + Slot6SessionEnd.Minute;

            bool t_asia = IsInSessionStrict(barTimeMins, aStart, aEnd);
            bool t_asia_alt = false;
            if (!t_asia) {
                int extEnd = (aEnd + bufferMins) % 1440;
                t_asia_alt = IsInSessionStrict(barTimeMins, aStart, extEnd) && !IsInSessionStrict(prevBarTimeMins, aStart, extEnd);
            }
            bool t_asia_final = t_asia ? t_asia : (UseAsia && t_asia_alt);

            bool t_london = IsInSessionStrict(barTimeMins, lStart, lEnd);
            bool t_london_alt = false;
            if (!t_london) {
                int extEnd = (lEnd + bufferMins) % 1440;
                t_london_alt = IsInSessionStrict(barTimeMins, lStart, extEnd) && !IsInSessionStrict(prevBarTimeMins, lStart, extEnd);
            }
            bool t_london_final = t_london ? t_london : (UseLondon && t_london_alt);

            bool t_ny = IsInSessionStrict(barTimeMins, nStart, nEnd);
            bool t_ny_alt = false;
            if (!t_ny) {
                int extEnd = (nEnd + bufferMins) % 1440;
                t_ny_alt = IsInSessionStrict(barTimeMins, nStart, extEnd) && !IsInSessionStrict(prevBarTimeMins, nStart, extEnd);
            }
            bool t_ny_final = t_ny ? t_ny : (UseNy && t_ny_alt);

            bool t_extra = IsInSessionStrict(barTimeMins, eStart, eEnd);
            bool t_extra_alt = false;
            if (!t_extra) {
                int extEnd = (eEnd + bufferMins) % 1440;
                t_extra_alt = IsInSessionStrict(barTimeMins, eStart, extEnd) && !IsInSessionStrict(prevBarTimeMins, eStart, extEnd);
            }
            bool t_extra_final = t_extra ? t_extra : (UseExtra && t_extra_alt);

            bool t_s5 = IsInSessionStrict(barTimeMins, s5Start, s5End);
            bool t_s5_alt = false;
            if (!t_s5) {
                int extEnd = (s5End + bufferMins) % 1440;
                t_s5_alt = IsInSessionStrict(barTimeMins, s5Start, extEnd) && !IsInSessionStrict(prevBarTimeMins, s5Start, extEnd);
            }
            bool t_s5_final = t_s5 ? t_s5 : (UseSlot5 && t_s5_alt);

            bool t_s6 = IsInSessionStrict(barTimeMins, s6Start, s6End);
            bool t_s6_alt = false;
            if (!t_s6) {
                int extEnd = (s6End + bufferMins) % 1440;
                t_s6_alt = IsInSessionStrict(barTimeMins, s6Start, extEnd) && !IsInSessionStrict(prevBarTimeMins, s6Start, extEnd);
            }
            bool t_s6_final = t_s6 ? t_s6 : (UseSlot6 && t_s6_alt);

            bool t_orb = (UseAsia && t_asia_final) || (UseLondon && t_london_final) || (UseNy && t_ny_final) || (UseExtra && t_extra_final) || (UseSlot5 && t_s5_final) || (UseSlot6 && t_s6_final);
            bool orbStart = t_orb && !prevWasInOrb;
            bool orbEnd = !t_orb && prevWasInOrb;

            if (orbStart && firstEvalOfThisBar)
            {
                if (entryOrder != null) { entryLimitSentAt = DateTime.MinValue; CancelOrder(entryOrder); }
                RemoveDrawObject("LIVE_FIRST_STRENGTH");
                effOrbReady = false;
                orbHigh = High[offset];
                orbLow = Low[offset];
                orbStartTime = barOpenTime;
                activeSessionId = (UseAsia && t_asia_final) ? 1
                    : (UseLondon && t_london_final) ? 2
                    : (UseNy && t_ny_final) ? 3
                    : (UseExtra && t_extra_final) ? 4
                    : (UseSlot5 && t_s5_final) ? 5
                    : (UseSlot6 && t_s6_final) ? 6 : 0;
                orbActive = true; orbComplete = false; trackingExpired = false;
                breakoutCount = 0;
                lastTradeBar = -1;
                hadBreakoutHigh = false; hadBreakoutLow = false;
                t1SlHit = false; t1SlLevel = double.NaN; t1WasLong = false;
                pendingT2Reversion = false; pendingT2QueuedAt = DateTime.MinValue;
                mtfAsiaHigh = double.NaN; mtfAsiaLow = double.NaN; mtfLondonHigh = double.NaN; mtfLondonLow = double.NaN; mtfNyHigh = double.NaN; mtfNyLow = double.NaN; mtfExtraHigh = double.NaN; mtfExtraLow = double.NaN;
                mtfS5High = double.NaN; mtfS5Low = double.NaN; mtfS6High = double.NaN; mtfS6Low = double.NaN;

                if (CloseOnNewOrb && Position.MarketPosition != MarketPosition.Flat)
                {
                    if (tvShadow) TvEndShadow("new ORB session");
                    tvSimActive = false; tvShadow = false;
                    if (SendClose("New ORB Session") && LogTiming)
                        OrbLog(string.Format("[ORB] NEW ORB SESSION CLOSE {0:HH:mm:ss.fff} PC | closing open trade at start of new ORB session", DateTime.Now));
                }

                if (Position.MarketPosition == MarketPosition.Flat)
                {
                    if (tvShadow) TvEndShadow("new ORB session");
                    tvSimActive = false; tvShadow = false;
                    currentEntry = double.NaN; initialSL = double.NaN; currentSL = double.NaN; currentTP = double.NaN;
                    backupSL = double.NaN; closeOrder = null; closingTrade = false; lastCloseSent = DateTime.MinValue;
                    exitCancelSent = false; exitCancelSentAt = DateTime.MinValue; restingExitFilledAt = DateTime.MinValue;
                    stopPlaced = false; needLineRedraw = false; entryFilledQty = 0;
                    highestPriceSinceEntry = double.MinValue; lowestPriceSinceEntry = double.MaxValue;
                    trailActivated = false; trailActivatedAt = DateTime.MinValue; trailBreachStart = DateTime.MinValue;

                    RemoveDrawObject("LIVE_TRADE_ENTRY"); RemoveDrawObject("LIVE_TRADE_SL"); RemoveDrawObject("LIVE_TRADE_TP");
                    RemoveDrawObject("LIVE_TRADE_SL_BOX"); RemoveDrawObject("LIVE_TRADE_TP_BOX"); RemoveDrawObject("LIVE_SIG_UP"); RemoveDrawObject("LIVE_SIG_DN");
                }
            }

            if (orbActive && t_orb)
            {
                orbHigh = Math.Max(orbHigh, High[offset]);
                orbLow = Math.Min(orbLow, Low[offset]);

                if (ShowOrbBox)
                {
                    Brush boxBrush = (UseSlot6 && t_s6_final) ? Brushes.Green : (UseSlot5 && t_s5_final) ? Brushes.Teal : (UseExtra && t_extra_final) ? Brushes.Orange : t_ny_final ? Brushes.Purple : t_london_final ? Brushes.Red : Brushes.Blue;
                    int areaOpacity = Math.Max(0, 100 - BoxTransparency);
                    Draw.Rectangle(this, "LIVE_ORB_BOX", true, orbStartTime, orbHigh, Time[offset], orbLow, Brushes.Transparent, boxBrush, areaOpacity);
                }
                if (ShowPivots)
                {
                    Brush lineBrush = ((UseSlot6 && t_s6_final) ? Brushes.Green : (UseSlot5 && t_s5_final) ? Brushes.Teal : (UseExtra && t_extra_final) ? Brushes.Orange : t_ny_final ? Brushes.Purple : t_london_final ? Brushes.Red : Brushes.Blue);
                    Draw.Line(this, "LIVE_ORB_HI", true, orbStartTime, orbHigh, Time[offset], orbHigh, lineBrush, DashStyleHelper.Solid, 1);
                    Draw.Line(this, "LIVE_ORB_LO", true, orbStartTime, orbLow, Time[offset], orbLow, lineBrush, DashStyleHelper.Solid, 1);
                    if (Show50Level)
                    {
                        double orb50 = (orbHigh + orbLow) / 2;
                        Draw.Line(this, "LIVE_ORB_50", true, orbStartTime, orb50, Time[offset], orb50, Brushes.Blue, DashStyleHelper.Dash, 1);
                    }
                }
            }

            if (orbEnd)
            {
                orbActive = false;
                orbComplete = true;
                orbEndBarClose = barCloseTime;
                effOrbHigh = orbHigh;
                effOrbLow = orbLow;
                effOrbReady = !double.IsNaN(orbHigh) && !double.IsNaN(orbLow);
            }

            if (UseOrbTfLock && orbBip > 0 && CurrentBars[orbBip] > 0)
            {
                bool inAsiaMtf = IsInSessionStrict(barTimeMins, aStart, aEnd);
                bool inLondonMtf = IsInSessionStrict(barTimeMins, lStart, lEnd);
                bool inNyMtf = IsInSessionStrict(barTimeMins, nStart, nEnd);
                bool inExtraMtf = IsInSessionStrict(barTimeMins, eStart, eEnd);
                bool inS5Mtf = IsInSessionStrict(barTimeMins, s5Start, s5End);
                bool inS6Mtf = IsInSessionStrict(barTimeMins, s6Start, s6End);

                if (wasMtfAsia && !inAsiaMtf && UseAsia && !double.IsNaN(mtfAsiaHigh)) { effOrbHigh = mtfAsiaHigh; effOrbLow = mtfAsiaLow; effOrbReady = true; }
                if (wasMtfLondon && !inLondonMtf && UseLondon && !double.IsNaN(mtfLondonHigh)) { effOrbHigh = mtfLondonHigh; effOrbLow = mtfLondonLow; effOrbReady = true; }
                if (wasMtfNy && !inNyMtf && UseNy && !double.IsNaN(mtfNyHigh)) { effOrbHigh = mtfNyHigh; effOrbLow = mtfNyLow; effOrbReady = true; }
                if (wasMtfExtra && !inExtraMtf && UseExtra && !double.IsNaN(mtfExtraHigh)) { effOrbHigh = mtfExtraHigh; effOrbLow = mtfExtraLow; effOrbReady = true; }
                if (wasMtfS5 && !inS5Mtf && UseSlot5 && !double.IsNaN(mtfS5High)) { effOrbHigh = mtfS5High; effOrbLow = mtfS5Low; effOrbReady = true; }
                if (wasMtfS6 && !inS6Mtf && UseSlot6 && !double.IsNaN(mtfS6High)) { effOrbHigh = mtfS6High; effOrbLow = mtfS6Low; effOrbReady = true; }
            }

            if (UseOrbTfLock && effOrbReady && orbComplete) { orbHigh = effOrbHigh; orbLow = effOrbLow; }
            if (orbComplete && orbStartTime != DateTime.MinValue) {
                if ((barOpenTime - orbStartTime).TotalHours > TrackingHours) trackingExpired = true;
            }

            double activeOrbHigh = (UseOrbTfLock && effOrbReady) ? effOrbHigh : orbHigh;
            double activeOrbLow = (UseOrbTfLock && effOrbReady) ? effOrbLow : orbLow;
            bool breakoutOrbComplete = UseOrbTfLock ? (orbComplete && effOrbReady) : orbComplete;
            bool chartTfOk = !UseOrbTfLock || BarsPeriod.Value <= OrbTfLockMinutes;

            if (breakoutOrbComplete && !t_orb && !double.IsNaN(activeOrbHigh) && !double.IsNaN(activeOrbLow))
            {
                bool priceAboveOrb = Close[offset] > activeOrbHigh;
                bool priceBelowOrb = Close[offset] < activeOrbLow;

                bool isFirstCandleAfterOrb = orbEnd || (barCloseTime == orbEndBarClose);
                bool firstCheck = AllowStrong1stThisSession() && isFirstCandleAfterOrb;
                bool strongFirstBull = false, strongFirstBear = false;
                double firstPct = 0.0;
                if (firstCheck && (priceAboveOrb || priceBelowOrb))
                {
                    bool bodyMode = StrongFirstMode == StrongMeasureType.CandleBody;
                    double bullDen = bodyMode ? Close[offset] - Open[offset] : High[offset] - Low[offset];
                    double bearDen = bodyMode ? Open[offset] - Close[offset] : High[offset] - Low[offset];
                    if (priceAboveOrb)
                    {
                        firstPct = bullDen > 0 ? Math.Min(100.0, (Close[offset] - activeOrbHigh) / bullDen * 100.0) : 0.0;
                        strongFirstBull = firstPct >= StrongFirstPct;
                    }
                    else
                    {
                        firstPct = bearDen > 0 ? Math.Min(100.0, (activeOrbLow - Close[offset]) / bearDen * 100.0) : 0.0;
                        strongFirstBear = firstPct >= StrongFirstPct;
                    }
                }
                bool pastOrbEnd = !isFirstCandleAfterOrb || strongFirstBull || strongFirstBear;

                bool isFlat = Position.MarketPosition == MarketPosition.Flat;
                int entryMins = barTimeMins + BarsPeriod.Value;
                bool flattenBlock = UseFlatten && entryMins >= (FlattenTime.Hour * 60 + FlattenTime.Minute) && entryMins < 18 * 60;

                // v2.7: Pine Script T1 SL-Hit Re-entry Gate
                // In Pine:
                // sl_reentry_long_ok  = (t1_sl_hit and not t1_was_long and not na(t1_sl_level)) ? (close > t1_sl_level) : price_above_orb
                // sl_reentry_short_ok = (t1_sl_hit and t1_was_long and not na(t1_sl_level))     ? (close < t1_sl_level) : price_below_orb
                // Also handle same-bar T1 SL hit where T1 is still open or just hit SL on this closing bar:
                bool t1OpenAndBreachedLong = !isFlat && breakoutCount == 1 && !currentIsLong && !trailActivated && !double.IsNaN(t1SlLevel) && Close[offset] > t1SlLevel;
                bool t1OpenAndBreachedShort = !isFlat && breakoutCount == 1 && currentIsLong && !trailActivated && !double.IsNaN(t1SlLevel) && Close[offset] < t1SlLevel;

                bool slReentryLongOk = ((t1SlHit || t1OpenAndBreachedLong) && !t1WasLong && !double.IsNaN(t1SlLevel))
                    ? (Close[offset] > t1SlLevel)
                    : priceAboveOrb;
                bool slReentryShortOk = ((t1SlHit || t1OpenAndBreachedShort) && t1WasLong && !double.IsNaN(t1SlLevel))
                    ? (Close[offset] < t1SlLevel)
                    : priceBelowOrb;

                // If T1 is still open on the bar close where price already closed beyond T1's SL level (like Trade #5 -> #6 at 09:00),
                // flatten T1 immediately and queue T2 so it fires the millisecond T1 is flat.
                bool baseCanTrade = allowEntries && !tradingHalted && breakoutCount < EffMaxBreakouts() && !trackingExpired && PassesFilters(nyOpenTime) && pastOrbEnd && chartTfOk && (lastTradeBar == -1 || CurrentBar - offset > lastTradeBar) && !flattenBlock;
                if (baseCanTrade && !isFlat && (t1OpenAndBreachedLong || t1OpenAndBreachedShort) && !pendingT2Reversion)
                {
                    t1SlHit = true;
                    pendingT2Reversion = true;
                    pendingT2IsLong = t1OpenAndBreachedLong;
                    pendingT2OrbHigh = activeOrbHigh;
                    pendingT2OrbLow = activeOrbLow;
                    pendingT2Offset = offset;
                    pendingT2QueuedAt = DateTime.Now;
                    if (tvShadow) TvEndShadow("T1 SL breached at bar close -> reversing to T2");
                    tvSimActive = false; tvShadow = false;
                    SendClose("ORB_T1_SL_Reversion");
                    if (LogTiming)
                        OrbLog(string.Format("[ORB] T1 SL -> T2 REVERSION QUEUED {0:HH:mm:ss.fff} PC | bar closed at {1} beyond T1 SL level {2} while T1 still open -> flattening T1 & queueing T2 {3}",
                            DateTime.Now, Close[offset], t1SlLevel, t1OpenAndBreachedLong ? "LONG" : "SHORT"));
                }

                // If T2 SL-reentry is valid, clear any lingering TV shadow from T1
                if (isFlat && t1SlHit && tvShadow && ((hadBreakoutLow && !currentIsLong && slReentryLongOk) || (hadBreakoutHigh && currentIsLong && slReentryShortOk)))
                {
                    TvEndShadow("T1 SL hit and T2 re-entry triggered");
                }

                bool tvStillInTrade = TvShadowOpen();
                bool canTakeBreakout = baseCanTrade && isFlat && entryOrder == null && !tvStillInTrade && !pendingT2Reversion;

                if (canTakeBreakout)
                {
                    bool isFirstHighBreak = !hadBreakoutHigh && breakoutCount == 0 && priceAboveOrb;
                    bool isReentryHighBreak = hadBreakoutLow && !currentIsLong && slReentryLongOk;
                    if (isFirstHighBreak || isReentryHighBreak)
                    {
                        TriggerEntry(true, offset, activeOrbHigh, activeOrbLow);
                        canTakeBreakout = false;
                    }
                }

                if (canTakeBreakout)
                {
                    bool isFirstLowBreak = !hadBreakoutLow && breakoutCount == 0 && priceBelowOrb;
                    bool isReentryLowBreak = hadBreakoutHigh && currentIsLong && slReentryShortOk;
                    if (isFirstLowBreak || isReentryLowBreak)
                    {
                        TriggerEntry(false, offset, activeOrbHigh, activeOrbLow);
                    }
                }

                if (allowEntries && firstCheck && (priceAboveOrb || priceBelowOrb))
                {
                    bool firstOk = priceAboveOrb ? strongFirstBull : strongFirstBear;
                    string firstTxt = string.Format("{0} 1st {1:0}% (need {2:0}%)", firstOk ? "STRONG" : "WEAK", firstPct, StrongFirstPct);
                    if (ShowStrongInfo)
                        Draw.Text(this, "LIVE_FIRST_STRENGTH", firstTxt, offset, priceAboveOrb ? High[offset] + TickSize * 10 : Low[offset] - TickSize * 10, firstOk ? Brushes.Green : Brushes.Gray);
                    if (LogTiming)
                        OrbLog(string.Format("[ORB] 1ST CANDLE {0} | {1} | close {2} | ORB high {3} / low {4} | {5}",
                            priceAboveOrb ? "UP" : "DOWN", firstTxt, Close[offset], activeOrbHigh, activeOrbLow, firstOk ? "allowed to enter on this candle" : "not strong enough -> waits for the next candle"));
                }

                if (ShowPivots && !trackingExpired)
                {
                    Brush lineBrush = ((UseSlot6 && t_s6_final) ? Brushes.Green : (UseSlot5 && t_s5_final) ? Brushes.Teal : (UseExtra && t_extra_final) ? Brushes.Orange : t_ny_final ? Brushes.Purple : t_london_final ? Brushes.Red : Brushes.Blue);
                    Draw.Line(this, "LIVE_ORB_HI", true, orbStartTime, activeOrbHigh, Time[offset], activeOrbHigh, lineBrush, DashStyleHelper.Solid, 1);
                    Draw.Line(this, "LIVE_ORB_LO", true, orbStartTime, activeOrbLow, Time[offset], activeOrbLow, lineBrush, DashStyleHelper.Solid, 1);
                    if (Show50Level)
                    {
                        double activeOrb50 = (activeOrbHigh + activeOrbLow) / 2;
                        Draw.Line(this, "LIVE_ORB_50", true, orbStartTime, activeOrb50, Time[offset], activeOrb50, Brushes.Blue, DashStyleHelper.Dash, 1);
                    }
                }
            }
            wasInOrb = t_orb;
        }

        private int EffMaxBreakouts()
        {
            if (Ny0600T1Only && activeSessionId == 5)
                return Math.Min(1, MaxBreakouts);
            return MaxBreakouts;
        }

        private bool AllowStrong1stThisSession()
        {
            if (!UseStrongFirst || !UseOrbTfLock) return false;
            if (SkipOvernight1stBar && (activeSessionId == 1 || activeSessionId == 2 || activeSessionId == 6))
                return false;
            return true;
        }

        private int TrailActTicks()
        {
            double slSize = TrailRefSlSize();
            return Math.Max(1, (int)Math.Round((slSize * TrailActR) / TickSize, MidpointRounding.AwayFromZero));
        }

        private int TrailOffTicks()
        {
            double slSize = TrailRefSlSize();
            return Math.Max(1, (int)Math.Round((slSize * TrailOffsetR) / TickSize, MidpointRounding.AwayFromZero));
        }

        private int lastHistoricalBar = -1;

        private bool IsStaleSignal(int offset)
        {
            if (State != State.Realtime || lastHistoricalBar < 0) return false;
            if (CurrentBar - offset > lastHistoricalBar) return false;
            int ms;
            return int.TryParse(CloseToNowMs(Time[offset]), out ms) && ms > 30000;
        }

        private void ReanchorToFinalBar()
        {
            timerTradeBar = -1;
            if (!ReanchorToFinal && !PhantomExit) return;
            if (double.IsNaN(plannedEntry) || double.IsNaN(plannedSL)) return;

            double fClose = Close[1];
            double fHigh = High[1];
            double fLow = Low[1];
            double fSL = currentIsLong
                ? (StopLossMode == SLModeType.ORBLevel ? entryOrbLow : fLow)
                : (StopLossMode == SLModeType.ORBLevel ? entryOrbHigh : fHigh);

            bool closeChanged = Math.Abs(fClose - plannedEntry) > TickSize / 2;
            bool slChanged = Math.Abs(fSL - plannedSL) > TickSize / 2;
            if (!closeChanged && !slChanged) return;

            double fSlSize = Math.Max(TickSize, Math.Abs(fClose - fSL));
            double fRisk1c = fSlSize * Instrument.MasterInstrument.PointValue;
            int fRaw = 0;
            if (fRisk1c > 0)
            {
                fRaw = RoundContractSize
                    ? (int)Math.Round(RiskPerTrade / fRisk1c, MidpointRounding.AwayFromZero)
                    : (int)Math.Floor(RiskPerTrade / fRisk1c);
            }
            int fQty = Math.Max(MinContracts, Math.Min(fRaw, MaxContracts));

            bool isT2SlReentry = (breakoutCount == 2 && prevT1SlHit && !double.IsNaN(prevT1SlLevel));
            bool stillBreakout = isT2SlReentry
                ? (currentIsLong ? fClose > prevT1SlLevel : fClose < prevT1SlLevel)
                : (currentIsLong ? fClose > entryOrbHigh : fClose < entryOrbLow);
            if (stillBreakout && !isT2SlReentry && Time[1] == orbEndBarClose)
            {
                if (!AllowStrong1stThisSession())
                {
                    stillBreakout = false;
                }
                else
                {
                    bool bodyMode = StrongFirstMode == StrongMeasureType.CandleBody;
                    double fDen = currentIsLong ? (bodyMode ? fClose - Open[1] : fHigh - fLow) : (bodyMode ? Open[1] - fClose : fHigh - fLow);
                    double fPct = fDen > 0 ? Math.Min(100.0, (currentIsLong ? (fClose - entryOrbHigh) : (entryOrbLow - fClose)) / fDen * 100.0) : 0.0;
                    if (fPct < StrongFirstPct) stillBreakout = false;
                }
            }
            bool zeroRisk = Math.Abs(fClose - fSL) < TickSize * 0.5;
            bool stopOk = !zeroRisk;
            bool riskOk = !SkipOversized || fRisk1c <= RiskPerTrade;
            bool tvWouldTrade = stillBreakout && stopOk && riskOk;

            if (LogTiming)
                OrbLog(string.Format("[ORB] REANCHOR {0:HH:mm:ss.fff} PC | timer close {1} -> final close {2} | SL {3} -> {4} | R {5:0.##} -> {6:0.##} pts | qty live {7} / TV {8} | TradingView would trade: {9}",
                    DateTime.Now, plannedEntry, fClose, plannedSL, fSL, plannedSlSize, fSlSize, plannedQty, fQty, tvWouldTrade ? "YES" : "NO"));

            if (!tvWouldTrade)
            {
                if (!PhantomExit) return;
                string why = !stillBreakout ? "final close back inside ORB" : (!stopOk ? "final stop is zero (SL == entry)" : "final 1-contract risk too high");
                if (!stillBreakout)
                {
                    breakoutCount = prevBreakoutCount;
                    hadBreakoutHigh = prevHadBreakoutHigh;
                    hadBreakoutLow = prevHadBreakoutLow;
                    t1SlHit = prevT1SlHit;
                    t1SlLevel = prevT1SlLevel;
                    t1WasLong = prevT1WasLong;
                    lastTradeBar = prevLastTradeBar;
                    restoreIsLongWhenFlat = true;
                }
                closingTrade = true;
                closeName = "ORB_Phantom_Exit";
                tvSimActive = false; tvShadow = false;
                if (entryOrder != null && !IsDone(entryOrder.OrderState)) CancelOrder(entryOrder);
                if (Position.MarketPosition != MarketPosition.Flat) SendClose(closeName);
                if (LogTiming)
                    OrbLog(string.Format("[ORB] PHANTOM EXIT {0:HH:mm:ss.fff} PC | {1} -> TradingView has no trade here -> closing", DateTime.Now, why));
                return;
            }

            if (!ReanchorToFinal) return;
            if (trailActivated)
            {
                if (LogTiming) OrbLog("[ORB] REANCHOR skipped | trail already active, levels kept");
                return;
            }

            plannedEntry = fClose;
            plannedSL = fSL;
            plannedSlSize = fSlSize;

            if (entryAtClosePhase && entryOrder != null && entryOrder.Filled == 0
                && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
            {
                plannedQty = fQty;
                double px = Instrument.MasterInstrument.RoundToTickSize(plannedEntry);
                if (currentIsLong) EnterLongLimit(0, true, plannedQty, px, "ORB_Entry");
                else EnterShortLimit(0, true, plannedQty, px, "ORB_Entry");
                if (LogTiming)
                    OrbLog(string.Format("[ORB] ENTRY LIMIT moved to final close {0} x{1}", px, plannedQty));
            }

            if (!RebaseToFill)
                initialSL = fSL;
            else if (entryFilledQty > 0)
                initialSL = Instrument.MasterInstrument.RoundToTickSize(currentIsLong ? currentEntry - plannedSlSize : currentEntry + plannedSlSize);
            currentSL = initialSL;
            if ((LocalTrail || TvEngine) && entryStopSent) backupSL = currentSL;
            if (TvEngine && tvSimActive && !tvSimActivated && !tvSimExited)
            {
                tvSimBest = TvRefEntry();
                tvSimLastPx = TvRefEntry();
            }

            double refEnt = TrailRefEntry();
            double slSize = TrailRefSlSize();
            if (TakeProfitMode == TPModeType.RiskReward) currentTP = currentIsLong ? refEnt + (slSize * RrRatio) : refEnt - (slSize * RrRatio);
            else if (TakeProfitMode == TPModeType.FixedPointsTicks) currentTP = currentIsLong ? refEnt + (TpTicks * TickSize) : refEnt - (TpTicks * TickSize);
            else currentTP = currentIsLong ? refEnt * (1 + (TpPercent / 100)) : refEnt * (1 - (TpPercent / 100));

            if (entryFilledQty > 0)
            {
                if (currentIsLong) highestPriceSinceEntry = Math.Max(Math.Max(refEnt, currentEntry), Close[0]);
                else lowestPriceSinceEntry = Math.Min(Math.Min(refEnt, currentEntry), Close[0]);
            }
            else
            {
                highestPriceSinceEntry = refEnt;
                lowestPriceSinceEntry = refEnt;
            }

            if (stopOrder != null && !IsDone(stopOrder.OrderState) && stopOrder.Filled == 0) stopSyncPending = true;
            if (tpOrder != null && !IsDone(tpOrder.OrderState) && tpOrder.Filled == 0) tpSyncPending = true;
            needLineRedraw = true;
        }

        private void TriggerEntry(bool isLong, int offset, double aHigh, double aLow)
        {
            if (Position.MarketPosition != MarketPosition.Flat) return;

            stopPlaced = false;
            needLineRedraw = false;
            entryFilledQty = 0;
            entryFillValue = 0;
            lastFlattenAttempt = DateTime.MinValue;
            lastStopChangeError = DateTime.MinValue;
            emergencyFlattenPending = false;
            trailActivated = false;
            trailActivatedAt = DateTime.MinValue;
            trailBreachStart = DateTime.MinValue;
            stopOrder = null;
            tpOrder = null;
            entryStopSent = false;
            stopSyncPending = false;
            tpSyncPending = false;
            backupSL = double.NaN;
            closeOrder = null;
            lastCloseSent = DateTime.MinValue;
            closingTrade = false;
            closeName = "";
            strayAttempts = 0;
            exitCancelSent = false;
            exitCancelSentAt = DateTime.MinValue;
            restingExitFilledAt = DateTime.MinValue;
            timerTradeBar = -1;
            postExitWatch = false;
            entryAtClosePhase = false;
            tvSimActive = false; tvSimExited = false; tvShadow = false;
            lastExitName = "";
            brokerStopAtTvLevel = false; tvLevelLogged = false;
            lastExitExecAt = DateTime.MinValue;
            prevBreakoutCount = breakoutCount;
            prevHadBreakoutHigh = hadBreakoutHigh;
            prevHadBreakoutLow = hadBreakoutLow;
            prevT1SlHit = t1SlHit;
            prevT1SlLevel = t1SlLevel;
            prevT1WasLong = t1WasLong;
            prevLastTradeBar = lastTradeBar;
            prevIsLong = currentIsLong;
            restoreIsLongWhenFlat = false;
            breakoutCount++; currentIsLong = isLong;
            tradeStartBar = CurrentBar - offset; lastTradeBar = tradeStartBar;
            if (isLong) hadBreakoutHigh = true; else hadBreakoutLow = true;

            // v2.7: Pine Script T1 SL-level recording
            if (breakoutCount == 1)
            {
                t1SlLevel = isLong ? Low[offset] : High[offset];
                t1WasLong = isLong;
                t1SlHit = false;
            }
            else
            {
                t1SlHit = false;
            }

            entryOrbHigh = aHigh;
            entryOrbLow = aLow;

            currentEntry = Close[offset];
            highestPriceSinceEntry = currentEntry;
            lowestPriceSinceEntry = currentEntry;

            if (isLong) initialSL = StopLossMode == SLModeType.ORBLevel ? aLow : Low[offset];
            else initialSL = StopLossMode == SLModeType.ORBLevel ? aHigh : High[offset];

            currentSL = initialSL;
            plannedSL = initialSL;
            double slSize = Math.Max(TickSize, Math.Abs(currentEntry - initialSL));

            if (TakeProfitMode == TPModeType.RiskReward) currentTP = isLong ? currentEntry + (slSize * RrRatio) : currentEntry - (slSize * RrRatio);
            else if (TakeProfitMode == TPModeType.FixedPointsTicks) currentTP = isLong ? currentEntry + (TpTicks * TickSize) : currentEntry - (TpTicks * TickSize);
            else currentTP = isLong ? currentEntry * (1 + (TpPercent / 100)) : currentEntry * (1 - (TpPercent / 100));

            plannedSlSize = slSize;
            plannedEntry = Close[offset];

            double riskPerContract = slSize * Instrument.MasterInstrument.PointValue;
            int rawQty = 0;
            if (riskPerContract > 0)
            {
                rawQty = RoundContractSize
                    ? (int)Math.Round(RiskPerTrade / riskPerContract, MidpointRounding.AwayFromZero)
                    : (int)Math.Floor(RiskPerTrade / riskPerContract);
            }
            int qty = Math.Max(MinContracts, Math.Min(rawQty, MaxContracts));

            bool zeroRisk = Math.Abs(currentEntry - initialSL) < TickSize * 0.5;
            if (zeroRisk)
            {
                if (LogTiming)
                    OrbLog(string.Format("[ORB] SKIPPED {0} | Zero risk (SL == Entry {1})", isLong ? "LONG" : "SHORT", currentEntry));
                return;
            }

            if (SkipOversized && riskPerContract > RiskPerTrade)
            {
                if (LogTiming)
                    OrbLog(string.Format("[ORB] SKIPPED {0} | 1 contract risks ${1:0.##} > Risk Per Trade ${2}", isLong ? "LONG" : "SHORT", riskPerContract, RiskPerTrade));
                return;
            }

            if (IsStaleSignal(offset))
            {
                OrbLog(string.Format("[ORB] SKIPPED {0} | signal candle closed {1:HH:mm:ss}, before strategy enabled", isLong ? "LONG" : "SHORT", Time[offset]));
                return;
            }

            plannedQty = qty;
            if (TvEngine) TvSimReset(Time[offset]);
            timerTradeBar = (offset == 0 && State == State.Realtime) ? CurrentBar : -1;

            liveTradeStartTime = Time[offset].AddMinutes(-BarsPeriod.Value);
            DateTime endTime = Time[offset].AddMinutes(BarsPeriod.Value * MaxTradeBars);

            if (EntryAtCloseMs > 0 && State == State.Realtime)
            {
                double closePx = Instrument.MasterInstrument.RoundToTickSize(Close[offset]);
                entryLimitSentAt = DateTime.Now;
                entryAtCloseSentAt = DateTime.Now;
                entryAtClosePhase = true;
                Order eo = isLong ? EnterLongLimit(0, true, qty, closePx, "ORB_Entry")
                                  : EnterShortLimit(0, true, qty, closePx, "ORB_Entry");
                if (eo != null) entryOrder = eo;
                if (LogTiming)
                    OrbLog(string.Format("[ORB] ENTRY LIMIT {0:HH:mm:ss.fff} PC | {1} x{2} at signal close {3} for {4} ms, then up to {5} ticks past the {6}",
                        DateTime.Now, isLong ? "BUY" : "SELL", qty, closePx, EntryAtCloseMs, EntryChaseTicks, isLong ? "ask" : "bid"));
            }
            else if (MaxEntrySlipTicks > 0 && State == State.Realtime)
            {
                double capPrice = Instrument.MasterInstrument.RoundToTickSize(isLong ? Close[offset] + MaxEntrySlipTicks * TickSize : Close[offset] - MaxEntrySlipTicks * TickSize);
                entryLimitSentAt = DateTime.Now;
                if (isLong) EnterLongLimit(0, true, qty, capPrice, "ORB_Entry");
                else EnterShortLimit(0, true, qty, capPrice, "ORB_Entry");
            }
            else
            {
                if (isLong) EnterLong(qty, "ORB_Entry");
                else EnterShort(qty, "ORB_Entry");
            }

            if (HighlightBreakout) BarBrushes[offset] = BreakoutCandleCol;

            if (LogTiming)
                OrbLog(string.Format("[ORB] SIGNAL {0} | via {1} | signal bar closed {2:HH:mm:ss} | order sent {3:HH:mm:ss.fff} PC | candle close -> order sent {4} ms | qty {5} (raw {6:0.##}) | planned SL dist {7}",
                    isLong ? "LONG" : "SHORT", offset == 0 ? "TIMER CLOSE" : "first tick of next bar", Time[offset], DateTime.Now, CloseToNowMs(Time[offset]), qty, riskPerContract > 0 ? RiskPerTrade / riskPerContract : 0, slSize));

            if (ShowTradeLines)
            {
                Draw.Line(this, "LIVE_TRADE_ENTRY", false, liveTradeStartTime, currentEntry, endTime, currentEntry, Brushes.Black, DashStyleHelper.Solid, 1);
                Draw.Line(this, "LIVE_TRADE_SL", false, liveTradeStartTime, initialSL, endTime, initialSL, Brushes.Red, DashStyleHelper.Dash, 1);
                Draw.Line(this, "LIVE_TRADE_TP", false, liveTradeStartTime, currentTP, endTime, currentTP, Brushes.Blue, DashStyleHelper.Solid, 2);
            }
            if (ShowSlFill) Draw.Rectangle(this, "LIVE_TRADE_SL_BOX", false, liveTradeStartTime, currentEntry, endTime, initialSL, Brushes.Transparent, Brushes.Red, 15);
            if (ShowTpFill) Draw.Rectangle(this, "LIVE_TRADE_TP_BOX", false, liveTradeStartTime, currentEntry, redrawEndTime(endTime), currentTP, Brushes.Transparent, Brushes.Blue, 15);

            if (isLong) Draw.TriangleUp(this, "LIVE_SIG_UP", true, Time[offset], Low[offset] - TickSize * 5, Brushes.Green);
            else Draw.TriangleDown(this, "LIVE_SIG_DN", true, Time[offset], High[offset] + TickSize * 5, Brushes.Red);
        }

        private DateTime redrawEndTime(DateTime endTime)
        {
            return endTime;
        }
    }
}