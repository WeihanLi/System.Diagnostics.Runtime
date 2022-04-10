﻿using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class ThreadPoolSchedulingParser : IEventParser<ThreadPoolSchedulingParser>, ThreadPoolSchedulingParser.Events.Verbose
{
    public event Action? Enqueue;
    public event Action? Dequeue;

    public string EventSourceName => FrameworkEventSource.Name;
    public EventKeywords Keywords => (EventKeywords)(FrameworkEventSource.Keywords.ThreadPool);
#if NETFRAMEWORK
#endif
    public void ProcessEvent(EventWrittenEventArgs e)
    {
        switch (e.EventId)
        {
            case FrameworkEventSource.EventId.ThreadPoolEnqueueWork:
                Enqueue?.Invoke();
                break;
            case FrameworkEventSource.EventId.ThreadPoolDequeueWork:
                Dequeue?.Invoke();
                break;
        }
    }

    public static class Events
    {
        public interface Verbose : IVerboseEvents
        {
            event Action Enqueue;
            event Action Dequeue;
        }
    }
}
