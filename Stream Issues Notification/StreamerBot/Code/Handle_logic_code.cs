using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

public class CPHInline
{
    //You can adjust section below
    private const bool ExecuteActions = true; //Set this to false if you don't want to execute actions in StreamerBot when either Dropped frames or Skipped frames happen 
    private const float AutohideDelay = 10; //time in seconds before we hide the bar in case there are no issues
    private const float MinimumTimeBetweenActionCalls = 5 * 60; //time in seconds between action calls in case of issues
    //Do not touch things below
    private const float CongestionThreshold = 0.001f; //Threshold from which we assume we dropped frames.
    private DateTime m_LastStreamOKTime = DateTime.UtcNow;
    private DateTime m_LastStreamIssueTime = DateTime.UtcNow;
    private DateTime m_LastActionCallDroppedFrames = DateTime.UtcNow;
    private DateTime m_LastActionCallSkippedFrames = DateTime.UtcNow;
    private IssueType m_LastIssueType = IssueType.Reconnecting;
    public RQ_SetStats m_CachedStats = new RQ_SetStats(); //To reduce the amount of allocations
    public long m_LastSkippedFrames;
    private string m_NotifyDroppedFramesID = null;
    private string m_NotifySkippedFramesID = null;
    public void Init()
    {
        List<Streamer.bot.Plugin.Interface.Model.ActionData> actions = CPH.GetActions();
        m_NotifyDroppedFramesID = default;
        m_NotifySkippedFramesID = default;
        foreach (Streamer.bot.Plugin.Interface.Model.ActionData action in actions)
        {
            if (action.Name == "Notify - Dropped frames")
            {
                m_NotifyDroppedFramesID = action.Id.ToString();
                CPH.LogInfo($"Found Dropped frames action: {m_NotifyDroppedFramesID}");
            }
            else if (action.Name == "Notify - Skipped frames")
            {
                m_NotifySkippedFramesID = action.Id.ToString();
                CPH.LogInfo($"Found skipped frames action: {m_NotifySkippedFramesID}");
            }
        }
    }

    public bool Execute()
    {
        //This is a bit hacky, since OBS ties skipped and dropped frames together
        //Technically both can result in stream buffering
        //But we definetely want to focus on dropped more
        //So we check congestion and we assume if it's below threshold you skip frames and if it's above, you drop them
        //Hopefully that's good enough
        _ = CPH.TryGetArg("webSocketAction", out string actionToPerform);
        switch (actionToPerform)
        {
            case "IntervalCheck":
                return PerformIntervalUpdate();
            case "SocketConnected":
                return SendCachedState();
            case "StreamStarted":
            case "StreamEnded":
                if (actionToPerform == "StreamStarted" && CPH.TryGetArg("OBS_BrowserSourceName", out string browserName))
                {
                    OBS_RefreshRequest refreshContent = new OBS_RefreshRequest();
                    refreshContent.inputName = browserName;
                    CPH.LogDebug(browserName);
                    CPH.ObsSendRaw("PressInputPropertiesButton", JsonConvert.SerializeObject(refreshContent));
                }

                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_Hide()));
                m_CachedStats = new RQ_SetStats();
                m_LastIssueType = IssueType.OK;
                m_LastStreamOKTime = DateTime.UtcNow;
                m_LastStreamIssueTime = DateTime.MinValue;
                return true;
            case "Testing":
                PerformTest();
                return true;
            default:
                return true;
        }
    }

    private void PerformTest()
    {
        if(!CPH.TryGetArg("testingAction", out string action))
        {
            CPH.ShowToastNotification(Guid.NewGuid().ToString(), "Error", "Failed to parse action name!", "StreamerBot", "");
            return;
        }

        CPH.ShowToastNotification(Guid.NewGuid().ToString(), "Testing action", action, "StreamerBot", "");

        switch(action)
        {
            case "SkippedFrames":
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.SkippedFrames }));
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStats()
                {
                    OutputCongestion = 0,
                    OutputSkippedFrames = 20,
                    OutputTotalFrames = 99999
                }));
                break;
            case "DroppedFramesLow":
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.DroppedFrames }));
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStats()
                {
                    OutputCongestion = 0.1f,
                    OutputSkippedFrames = 20,
                    OutputTotalFrames = 99999
                }));
                break;
            case "DroppedFramesMedium":
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.DroppedFrames }));
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStats()
                {
                    OutputCongestion = 0.3f,
                    OutputSkippedFrames = 20,
                    OutputTotalFrames = 99999
                }));
                break;
            case "DroppedFramesHigh":
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.DroppedFrames }));
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStats()
                {
                    OutputCongestion = 0.6f,
                    OutputSkippedFrames = 20,
                    OutputTotalFrames = 99999
                }));
                break;
            case "DroppedFramesExtreme":
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.DroppedFrames }));
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStats()
                {
                    OutputCongestion = 0.95f,
                    OutputSkippedFrames = 20,
                    OutputTotalFrames = 99999
                }));
                break;
            case "Normal":
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.OK }));
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_Hide()));
                break;
        }
    }

    public bool SendCachedState()
    {
        switch (m_LastIssueType)
        {
            case IssueType.OK:
            case IssueType.NoOBS:
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_Hide()));
                break;
            case IssueType.Reconnecting:
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.Reconnecting }));
                break;
            case IssueType.DroppedFrames:
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.DroppedFrames }));
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(m_CachedStats));
                break;
            case IssueType.SkippedFrames:
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.SkippedFrames }));
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(m_CachedStats));
                break;
        }

        return true;
    }

    public bool PerformIntervalUpdate()
    {
        var obsRaw = JsonConvert.DeserializeObject<OBS_Status>(CPH.ObsSendRaw("GetStreamStatus", ""));
        if (obsRaw == null)
        {
            CPH.LogError("Failed to deserialize OBS status!");

            m_LastStreamOKTime = DateTime.UtcNow;
            m_LastStreamIssueTime = DateTime.MinValue;
            if (m_LastIssueType != IssueType.OK)
            {
                m_LastIssueType = IssueType.OK;
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_Hide()));
            }

            return false;
        }

        if(!obsRaw.outputActive)
        {
            m_LastStreamOKTime = DateTime.UtcNow;
            m_LastStreamIssueTime = DateTime.MinValue;
            if (m_LastIssueType != IssueType.OK)
            {
                m_LastIssueType = IssueType.OK;
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_Hide()));
            }

            return false;
        }

        if (obsRaw.outputReconnecting)
        {
            m_LastStreamIssueTime = DateTime.UtcNow;
            if (m_LastIssueType != IssueType.Reconnecting)
            {
                m_LastIssueType = IssueType.Reconnecting;
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.Reconnecting }));
            }

            m_LastStreamIssueTime = DateTime.UtcNow;
            return false;
        }

        //Give like 5 seconds to stabilize before we start checking
        if (obsRaw.outputDuration < 5_000)
            return false;
        
        m_CachedStats.OutputCongestion = obsRaw.outputCongestion;
        m_CachedStats.OutputSkippedFrames = obsRaw.outputSkippedFrames;
        m_CachedStats.OutputTotalFrames = obsRaw.outputTotalFrames;

        //SendFullInformation();
        if (m_CachedStats.OutputCongestion < CongestionThreshold)
        {
            if (m_CachedStats.OutputSkippedFrames > m_LastSkippedFrames)
            {
                m_LastIssueType = IssueType.SkippedFrames;
                m_LastStreamIssueTime = DateTime.UtcNow;
                //We make dropped frames a priority
                if (m_LastIssueType != IssueType.DroppedFrames)
                {
                    if (m_LastIssueType != IssueType.SkippedFrames)
                    {
                        CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.SkippedFrames }));
                        m_LastIssueType = IssueType.SkippedFrames;
                        if (ExecuteActions && m_NotifySkippedFramesID != null)
                        {
                            if (m_LastActionCallSkippedFrames + TimeSpan.FromSeconds(MinimumTimeBetweenActionCalls) < DateTime.UtcNow)
                            {
                                CPH.RunActionById(m_NotifySkippedFramesID, false);
                                m_LastActionCallSkippedFrames = DateTime.UtcNow;
                            }
                        }
                    }
                }

                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(m_CachedStats));
            }
            else
            {
                if (m_LastIssueType != IssueType.OK)
                    CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(m_CachedStats));
                m_LastStreamOKTime = DateTime.UtcNow;
            }
        }
        else
        {
            //Dropped something... probably
            if (m_CachedStats.OutputSkippedFrames > m_LastSkippedFrames)
            {
                if (m_LastIssueType != IssueType.DroppedFrames)
                {
                    CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_SetStyle() { Style = IssueType.DroppedFrames }));
                    m_LastIssueType = IssueType.DroppedFrames;
                    if (ExecuteActions && m_NotifyDroppedFramesID != null)
                    {
                        CPH.RunActionById(m_NotifyDroppedFramesID, false);
                    }
                }

                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(m_CachedStats));
                m_LastStreamIssueTime = DateTime.UtcNow;
            }
            else
            {
                if (m_LastIssueType != IssueType.OK)
                {
                    if (m_LastActionCallDroppedFrames + TimeSpan.FromSeconds(MinimumTimeBetweenActionCalls) < DateTime.UtcNow)
                    {
                        CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(m_CachedStats));
                        m_LastActionCallDroppedFrames = DateTime.UtcNow;
                    }
                }

                m_LastStreamOKTime = DateTime.UtcNow;
            }
        }

        if ((m_LastStreamOKTime - m_LastStreamIssueTime).TotalSeconds > AutohideDelay)
        {
            if (m_LastIssueType != IssueType.OK)
            {
                m_LastIssueType = IssueType.OK;
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(new RQ_Hide()));
            }
        }

        m_LastSkippedFrames = m_CachedStats.OutputSkippedFrames;
        return true;
    }

    //To reduce amount of data we send
    public enum IssueType
    {
        OK,
        NoOBS,
        Reconnecting,
        DroppedFrames,
        SkippedFrames,
    }

    public class OBS_Status
    {
        public bool outputActive;
        public long outputBytes;
        public float outputCongestion;
        public long outputDuration;
        public bool outputReconnecting;
        public long outputSkippedFrames;
        public TimeSpan outputTimecode;
        public long outputTotalFrames;
    }

    public class RQ_SetStyle
    {
        public string Type = "StreamStatusUpdate_SetStyle";
        [JsonConverter(typeof(StringEnumConverter))]
        public IssueType Style;
    }

    public class RQ_SetStats
    {
        public string Type = "StreamStatusUpdate_SetStats";
        public float OutputCongestion;
        public long OutputSkippedFrames;
        public long OutputTotalFrames;
    }

    public class RQ_ConnectionStatusData
    {
        public string Type = "StreamStatusUpdate_ConnectionStatus";
        public bool Active;
        public bool Reconnecting;
    }

    public class RQ_Hide
    {
        public string Type = "StreamStatusUpdate_Hide";
    }

    public class OBS_RefreshRequest
    {
        public string inputName = "";
        public string propertyName = "refreshnocache";
    }
}