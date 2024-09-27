#if !NETFRAMEWORK
using System.Diagnostics.Runtime.EventListening.Sources;
using System.Diagnostics.Runtime.Util;
using System.Diagnostics.Tracing;

namespace System.Diagnostics.Runtime.EventListening.Parsers;

public class NativeRuntimeEventParser : NativeEvent.INativeEvent, IEventListener
{
#if !NET7_0_OR_GREATER
    // flags representing the "Garbage Collection" + "Preparation for garbage collection" pause reasons
    private const uint SuspendGcReasons = 0x1 | 0x6;
#endif
    private readonly EventPairTimer<long> _eventPairTimer = new(
        NativeRuntimeEventSource.EventId.ContentionStart,
        NativeRuntimeEventSource.EventId.ContentionStop,
        x => x.OSThreadId);

    private readonly EventPairTimer<uint> _gcEventTimer = new(
        NativeRuntimeEventSource.EventId.GcStart,
        NativeRuntimeEventSource.EventId.GcEnd,
        x => (uint)x.Payload![0]!);
#if !NET7_0_OR_GREATER
    private readonly EventPairTimer<int> _gcPauseEventTimer = new(
        NativeRuntimeEventSource.EventId.SuspendEE,
        NativeRuntimeEventSource.EventId.RestartEEEnd,
        // Suspensions/ Resumptions are always done sequentially so there is no common value to match events on. Return a constant value as the event id.
        _ => 1);
#endif
    public event Action<NativeEvent.ContentionEndEvent>? ContentionEnd;
    public event Action<NativeEvent.HeapStatsEvent>? HeapStats;
#if !NET7_0_OR_GREATER
    public event Action<NativeEvent.PauseCompleteEvent>? PauseComplete;
#endif
    public event Action<NativeEvent.CollectionStartEvent>? CollectionStart;
    public event Action<NativeEvent.CollectionCompleteEvent>? CollectionComplete;
    public event Action<NativeEvent.AllocationTickEvent>? AllocationTick;
#if NETFRAMEWORK
    public event Action<NativeEvent.HeapFragmentationEvent>? HeapFragmentation;
#endif
    public event Action<NativeEvent.ThreadPoolAdjustedReasonEvent>? ThreadPoolAdjusted;

    public string EventSourceName => NativeRuntimeEventSource.Name;

    public EventKeywords Keywords => (EventKeywords)(
        NativeRuntimeEventSource.Keywords.Contention | // thread contention timing
        NativeRuntimeEventSource.Keywords.GC | // garbage collector details
        NativeRuntimeEventSource.Keywords.Threading); // threadpool events

    public void ProcessEvent(EventWrittenEventArgs e)
    {
        switch (e.EventId)
        {
            case NativeRuntimeEventSource.EventId.AllocTick:
            {
                const uint lohHeapFlag = 0x1;

                AllocationTick?.Invoke(new((uint)e.Payload![0]!, ((uint)e.Payload![1]! & lohHeapFlag) == lohHeapFlag));

                return;
            }
            case NativeRuntimeEventSource.EventId.HeapStats:
                HeapStats?.Invoke(new(e));

                return;
            case NativeRuntimeEventSource.EventId.ContentionStart:
            case NativeRuntimeEventSource.EventId.ContentionStop:
            {
                if (_eventPairTimer.TryGetDuration(e, out var duration) == DurationResult.FinalWithDuration &&
                    duration > TimeSpan.Zero &&
                    (byte)e.Payload![0]! == 0)
                    ContentionEnd?.Invoke(new(duration));

                return;
            }
#if !NET7_0_OR_GREATER
            case NativeRuntimeEventSource.EventId.SuspendEE:
            case NativeRuntimeEventSource.EventId.RestartEEEnd:
            {
                // Execution engine is pausing for a reason other than GC, discard event.
                if ((e.EventId != NativeRuntimeEventSource.EventId.SuspendEE ||
                     ((uint)e.Payload![0]! & SuspendGcReasons) != 0) &&
                    _gcPauseEventTimer.TryGetDuration(e, out var pauseDuration) == DurationResult.FinalWithDuration &&
                    pauseDuration > TimeSpan.Zero)
                    PauseComplete?.Invoke(new(pauseDuration));

                return;
            }
#endif
            case NativeRuntimeEventSource.EventId.GcStart or NativeRuntimeEventSource.EventId.GcEnd:
                switch (_gcEventTimer.TryGetDuration(e, out var gcDuration, out _))
                {
                    case DurationResult.Start:
                        CollectionStart?.Invoke(new((uint)e.Payload![1]!,
                            (NativeRuntimeEventSource.GCReason)e.Payload![2]!,
                            (NativeRuntimeEventSource.GCType)e.Payload![3]!));

                        break;
                    case DurationResult.FinalWithDuration when gcDuration > TimeSpan.Zero:
                        CollectionComplete?.Invoke(new((uint)e.Payload![1]!, gcDuration));
                        break;
                }

                return;
            case NativeRuntimeEventSource.EventId.ThreadPoolAdjustment:
                ThreadPoolAdjusted?.Invoke(new((uint)e.Payload![1]!,
                    (NativeRuntimeEventSource.ThreadAdjustmentReason)e.Payload![2]!));

                return;
        }
    }
}
#endif
