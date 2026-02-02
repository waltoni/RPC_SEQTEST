#region Using directives
using System;
using CoreBase = FTOptix.CoreBase;
using FTOptix.HMIProject;
using UAManagedCore;
using FTOptix.UI;
using FTOptix.NetLogic;
#endregion

public class ClockLogic : BaseNetLogic
{
	public override void Start()
	{
		// Cache all variable references once at startup to avoid repeated GetVariable calls
		timeVar = LogicObject.GetVariable("Time");
		timeYearVar = LogicObject.GetVariable("Time/Year");
		timeMonthVar = LogicObject.GetVariable("Time/Month");
		timeDayVar = LogicObject.GetVariable("Time/Day");
		timeHourVar = LogicObject.GetVariable("Time/Hour");
		timeMinuteVar = LogicObject.GetVariable("Time/Minute");
		timeSecondVar = LogicObject.GetVariable("Time/Second");
		utcTimeVar = LogicObject.GetVariable("UTCTime");
		utcTimeYearVar = LogicObject.GetVariable("UTCTime/Year");
		utcTimeMonthVar = LogicObject.GetVariable("UTCTime/Month");
		utcTimeDayVar = LogicObject.GetVariable("UTCTime/Day");
		utcTimeHourVar = LogicObject.GetVariable("UTCTime/Hour");
		utcTimeMinuteVar = LogicObject.GetVariable("UTCTime/Minute");
		utcTimeSecondVar = LogicObject.GetVariable("UTCTime/Second");

		periodicTask = new PeriodicTask(UpdateTime, 1000, LogicObject);
		periodicTask.Start();
	}

	public override void Stop()
	{
		periodicTask?.Dispose();
		periodicTask = null;
		
		// Clear cached references
		timeVar = null;
		timeYearVar = null;
		timeMonthVar = null;
		timeDayVar = null;
		timeHourVar = null;
		timeMinuteVar = null;
		timeSecondVar = null;
		utcTimeVar = null;
		utcTimeYearVar = null;
		utcTimeMonthVar = null;
		utcTimeDayVar = null;
		utcTimeHourVar = null;
		utcTimeMinuteVar = null;
		utcTimeSecondVar = null;
	}

	private void UpdateTime()
	{
		DateTime localTime = DateTime.Now;
		DateTime utcTime = DateTime.UtcNow;
		
		// Use cached variable references instead of GetVariable calls
		timeVar.Value = localTime;
		timeYearVar.Value = localTime.Year;
		timeMonthVar.Value = localTime.Month;
		timeDayVar.Value = localTime.Day;
		timeHourVar.Value = localTime.Hour;
		timeMinuteVar.Value = localTime.Minute;
		timeSecondVar.Value = localTime.Second;
		utcTimeVar.Value = utcTime;
		utcTimeYearVar.Value = utcTime.Year;
		utcTimeMonthVar.Value = utcTime.Month;
		utcTimeDayVar.Value = utcTime.Day;
		utcTimeHourVar.Value = utcTime.Hour;
		utcTimeMinuteVar.Value = utcTime.Minute;
		utcTimeSecondVar.Value = utcTime.Second;
	}

	private PeriodicTask periodicTask;
	
	// Cached variable references to avoid repeated GetVariable calls
	private IUAVariable timeVar;
	private IUAVariable timeYearVar;
	private IUAVariable timeMonthVar;
	private IUAVariable timeDayVar;
	private IUAVariable timeHourVar;
	private IUAVariable timeMinuteVar;
	private IUAVariable timeSecondVar;
	private IUAVariable utcTimeVar;
	private IUAVariable utcTimeYearVar;
	private IUAVariable utcTimeMonthVar;
	private IUAVariable utcTimeDayVar;
	private IUAVariable utcTimeHourVar;
	private IUAVariable utcTimeMinuteVar;
	private IUAVariable utcTimeSecondVar;
}
