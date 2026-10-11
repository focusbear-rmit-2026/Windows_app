using System;
using System.Collections.Generic;
using System.Linq;

namespace FocusBear.Core.ToDo
{
    /// <summary>How a run of brain-dump writes ended.</summary>
    public enum BrainDumpWriteOutcome
    {
        /// <summary>Every task was confirmed saved.</summary>
        Complete,

        /// <summary>A write was not confirmed. <see cref="BrainDumpWriteResult.ConfirmedCount"/> says
        /// how many got through before it.</summary>
        Failed,

        /// <summary>The run no longer applies — the draft was dismissed, or the account changed under
        /// it.</summary>
        Superseded,
    }

    /// <summary>The result of one <see cref="BrainDumpTaskWriter"/> run.</summary>
    public struct BrainDumpWriteResult : IEquatable<BrainDumpWriteResult>
    {
        private BrainDumpWriteResult(BrainDumpWriteOutcome outcome, int confirmedCount)
        {
            Outcome = outcome;
            ConfirmedCount = confirmedCount;
        }

        public BrainDumpWriteOutcome Outcome { get; }

        /// <summary>How many tasks the server confirmed. Only meaningful for
        /// <see cref="BrainDumpWriteOutcome.Failed"/>.</summary>
        public int ConfirmedCount { get; }

        public static readonly BrainDumpWriteResult Complete =
            new BrainDumpWriteResult(BrainDumpWriteOutcome.Complete, 0);

        public static readonly BrainDumpWriteResult Superseded =
            new BrainDumpWriteResult(BrainDumpWriteOutcome.Superseded, 0);

        public static BrainDumpWriteResult Failed(int confirmedCount)
        {
            return new BrainDumpWriteResult(BrainDumpWriteOutcome.Failed, confirmedCount);
        }

        public bool Equals(BrainDumpWriteResult other)
        {
            return Outcome == other.Outcome && ConfirmedCount == other.ConfirmedCount;
        }

        public override bool Equals(object obj)
        {
            return obj is BrainDumpWriteResult && Equals((BrainDumpWriteResult)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return ((int)Outcome * 397) ^ ConfirmedCount; }
        }

        public override string ToString()
        {
            return Outcome == BrainDumpWriteOutcome.Failed
                ? "Failed(confirmed: " + ConfirmedCount + ")"
                : Outcome.ToString();
        }
    }

    /// <summary>
    /// Saves a reviewed brain dump one task at a time, SEQUENTIALLY, and stops at the first
    /// unconfirmed response.
    ///
    /// Firing the whole list at once would be faster and is the obvious implementation; it is wrong
    /// for two reasons the sequential form fixes together:
    ///
    /// <list type="bullet">
    /// <item>Only tasks the server CONFIRMED are removed from the review draft. A parallel run that
    /// half-succeeds leaves the user with no way to tell which half, so "retry" either duplicates
    /// work or drops it. Here the draft keeps exactly the tasks that did not land, and retrying it is
    /// an upsert of the same client-generated ids.</item>
    /// <item>The account (or the draft) can change mid-run. Each step re-asks
    /// <c>isCurrent</c> before it writes and again before it records a save, so a sign-out cannot keep
    /// pushing the previous user's tasks, and a dismissed draft cannot be edited by a response that
    /// arrives after it closed.</item>
    /// </list>
    ///
    /// A duplicate or late callback is ignored rather than trusted: <c>_awaitingResponse</c> plus the
    /// captured index mean a handler called twice cannot advance two tasks. The Mac's test for that
    /// is ported verbatim.
    ///
    /// Not thread-safe, and deliberately so: the Mac drives this from the main queue, and the Windows
    /// host must do the same (the UI thread) rather than adding a lock that would hide a caller doing
    /// something else.
    /// </summary>
    public sealed class BrainDumpTaskWriter
    {
        private readonly IList<BrainDumpTask> _tasks;
        private readonly Func<bool> _isCurrent;
        private readonly Action<BrainDumpTask, Action<bool>> _write;
        private readonly Action<string> _didSave;
        private readonly Action<BrainDumpWriteResult> _completion;

        private int _index;
        private bool _awaitingResponse;
        private bool _finished;

        /// <param name="isCurrent">Whether this run still applies — the account/draft generation check.</param>
        /// <param name="write">Performs one write, calling back with whether the server confirmed it.</param>
        /// <param name="didSave">Called with the id of each CONFIRMED task, so the draft can drop it.</param>
        public BrainDumpTaskWriter(IEnumerable<BrainDumpTask> tasks, Func<bool> isCurrent,
                                   Action<BrainDumpTask, Action<bool>> write, Action<string> didSave,
                                   Action<BrainDumpWriteResult> completion)
        {
            _tasks = (tasks ?? Enumerable.Empty<BrainDumpTask>()).ToList();
            _isCurrent = isCurrent;
            _write = write;
            _didSave = didSave;
            _completion = completion;
        }

        public void Start()
        {
            Advance();
        }

        /// <summary>The draft was dismissed. Any response still in flight is ignored.</summary>
        public void Cancel()
        {
            Finish(BrainDumpWriteResult.Superseded);
        }

        private void Advance()
        {
            if (_finished || _awaitingResponse) return;
            if (!_isCurrent())
            {
                Finish(BrainDumpWriteResult.Superseded);
                return;
            }
            if (_index >= _tasks.Count)
            {
                Finish(BrainDumpWriteResult.Complete);
                return;
            }

            int requestIndex = _index;
            BrainDumpTask task = _tasks[_index];
            _awaitingResponse = true;
            _write(task, confirmed =>
            {
                // A second call for the same write, or one arriving after the run ended, changes nothing.
                if (_finished || !_awaitingResponse || _index != requestIndex) return;
                _awaitingResponse = false;

                // Re-checked AFTER the response as well as before it: the sign-out may have happened
                // while this write was in flight, and the draft is no longer ours to edit.
                if (!_isCurrent())
                {
                    Finish(BrainDumpWriteResult.Superseded);
                    return;
                }
                if (!confirmed)
                {
                    Finish(BrainDumpWriteResult.Failed(_index));
                    return;
                }

                _didSave(task.Id);
                _index++;
                Advance();
            });
        }

        private void Finish(BrainDumpWriteResult result)
        {
            if (_finished) return;
            _finished = true;
            _completion(result);
        }
    }
}
