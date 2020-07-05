/**
 *
 *  ISEL, LEIC, Concurrent Programming
 *
 *  Semaphore with asynchronous and synchronous interface
 *
 *  Carlos Martins, June 2020
 *
 **/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncServerClient.MIX
{
    public class SemaphoreAsync
    {
        // The type used to hold each async acquire request
        private class AsyncAcquire : TaskCompletionSource<bool>
        {
            internal readonly int acquires; // the number of requested permits
            internal readonly CancellationToken cToken; // cancellation token
            internal CancellationTokenRegistration cTokenRegistration; // used to dispose the cancellation handler 
            internal Timer timer;
            internal bool done; // true when the async request is completed or canceled

            internal AsyncAcquire(int acquires, CancellationToken cToken) : base()
            {
                this.acquires = acquires;
                this.cToken = cToken;
            }

            /**
		 * Disposes resources associated with this async acquire.
		 *
		 * Note: when this method is called we are sure that the field "timer" is correctly affected,
		 *		 but we are not sure if the "cTokenRegistration" field is.
		 * 		 However, this does not cause any damage, because when this method is called by
		 *	     cancellation handler this field is not used, as the resources mobilized to register
		 *		 the handler are released after its invocation.
		 */
            internal void Dispose(bool canceling = false)
            {
                if (!canceling && cToken.CanBeCanceled)
                    cTokenRegistration.Dispose();
                timer?.Dispose();
            }
        }

        // The lock - we do not use the monitor functionality
        private readonly object theLock = new object();

        // available and maximum number of permits	
        private int permits;
        private readonly int maxPermits;

        // The queue of pending asynchronous requests.
        private readonly LinkedList<AsyncAcquire> asyncAcquires;


        /**
	 * Delegates used as cancellation handlers for asynchrounous requests 
	 */
        private readonly Action<object> cancellationHandler;

        private readonly TimerCallback timeoutHandler;

        /**
	 *  Completed tasks use to return constant results from the AcquireAsync method
	 */
        private static readonly Task<bool> trueTask = Task.FromResult<bool>(true);

        private static readonly Task<bool> falseTask = Task.FromResult<bool>(false);

        private static readonly Task<bool> argExceptionTask =
            Task.FromException<bool>(new ArgumentException("acquires"));

        /**
	 * Constructor
	 */
        public SemaphoreAsync(int initial = 0, int maximum = Int32.MaxValue)
        {
            // Validate arguments
            if (initial < 0 || initial > maximum)
                throw new ArgumentOutOfRangeException("initial");
            if (maximum <= 0)
                throw new ArgumentOutOfRangeException("maximum");

            // Construct delegates used to describe the two cancellation handlers.
            cancellationHandler = new Action<object>((acquireNode) => AcquireCancellationHandler(acquireNode, true));
            timeoutHandler = new TimerCallback((acquireNode) => AcquireCancellationHandler(acquireNode, false));

            // Initialize the maximum number of permits - immutable
            maxPermits = maximum;

            // Initialize the shared mutable state
            permits = initial;
            asyncAcquires = new LinkedList<AsyncAcquire>();
        }

        /**
	 * Auxiliary methods
	 */
        /**
         * Returns the list of all pending async acquires that can be satisfied with
         * the number of permits currently owned by the semaphore.
         *
         * Note: This method is called when the current thread owns the lock.
         */
        private List<AsyncAcquire> SatisfyPendingAsyncAcquires()
        {
            List<AsyncAcquire> satisfied = null;
            while (asyncAcquires.Count > 0)
            {
                AsyncAcquire acquire = asyncAcquires.First.Value;
                // Check if available permits allow satisfy this async request
                if (acquire.acquires > permits)
                    break;
                // Remove the async request from the queue
                asyncAcquires.RemoveFirst();

                // Update permits and mark acquire as done
                permits -= acquire.acquires;
                acquire.done = true;
                // Add the async acquire to the result list
                if (satisfied == null)
                    satisfied = new List<AsyncAcquire>(1);
                satisfied.Add(acquire);
            }

            return satisfied;
        }

        /**
	 * Complete the tasks associated to the satisfied async acquire requests.
	 *
	 *  Note: This method is called when the current thread **does not own the lock**.
	 */
        private void CompleteSatisfiedAsyncAcquires(List<AsyncAcquire> toComplete)
        {
            if (toComplete != null)
            {
                foreach (AsyncAcquire acquire in toComplete)
                {
                    // Dispose the resources associated with the async acquirer and
                    // complete its task with success.
                    acquire.Dispose();
                    acquire.SetResult(true); // complete the associated request's task
                }
            }
        }

        /**
	 * Try to cancel an async acquire request
	 */
        private void AcquireCancellationHandler(object _acquireNode, bool canceling)
        {
            LinkedListNode<AsyncAcquire> acquireNode = (LinkedListNode<AsyncAcquire>) _acquireNode;
            AsyncAcquire acquire = acquireNode.Value;
            bool complete = false;
            List<AsyncAcquire> satisfied = null;

            // To access shared mutable state we must acquire the lock
            lock (theLock)
            {
                /**
                 * Here, the async request can be already satisfied or cancelled.
                 */
                if (!acquire.done)
                {
                    // Remove the async acquire request from queue and mark it as done.
                    asyncAcquires.Remove(acquireNode);
                    complete = acquire.done = true;

                    // If after removing the async acquire is possible to satisfy any
                    // pending async acquire(s) do it 
                    if (asyncAcquires.Count > 0 && permits >= asyncAcquires.First.Value.acquires)
                        satisfied = SatisfyPendingAsyncAcquires();
                }
            }

            // If we cancelled the async acquire, release the resources associated with it,
            // and complete the underlying task.
            if (complete)
            {
                // Complete any satisfied async acquires
                if (satisfied != null)
                    CompleteSatisfiedAsyncAcquires(satisfied);

                // Dispose the resources associated with the cancelled async acquire
                acquire.Dispose(canceling);

                // Complete the TaskCompletionSource to RanToCompletion with false (timeout)
                // or Canceled final state (cancellation).
                if (canceling)
                    acquire.SetCanceled(); // cancelled
                else
                    acquire.SetResult(false); // timeout
            }
        }

        /**
	 * Asynchronous Task-based Asynchronous Pattern (TAP) interface.
	 */
        /**
         * Acquires one or more permits asynchronously enabling, optionally,
         * a timeout and/or cancellation.
        */
        public Task<bool> AcquireAsync(int acquires = 1, int timeout = Timeout.Infinite,
            CancellationToken cToken = default(CancellationToken))
        {
            // Validate the argument "acquires"
            if (acquires < 1 || acquires > maxPermits)
                return argExceptionTask;
            lock (theLock)
            {
                // If the queue is empty ans sufficiente authorizations are available,
                // the acquire can be satisfied immediatelly; so, the field permits is
                // updated and a completed task is returned with a result of true.
                if (asyncAcquires.Count == 0 && permits >= acquires)
                {
                    permits -= acquires;
                    return trueTask;
                }

                // If the acquire was specified as immediate, return completed task with
                // a result of false, which means timeout.
                if (timeout == 0)
                    return falseTask;

                // If the cancellation was already requested return a a completed task in
                // the Canceled state
                if (cToken.IsCancellationRequested)
                    return Task.FromCanceled<bool>(cToken);

                // Create a request node and insert it in requests queue
                AsyncAcquire acquire = new AsyncAcquire(acquires, cToken);
                LinkedListNode<AsyncAcquire> acquireNode = asyncAcquires.AddLast(acquire);

                /**
                 * Activate the specified cancelers when owning the lock.
                 */

                /**
                 * Since the timeout handler, that runs on a thread pool's worker thread,
                 * that acquires the lock before access the fields "acquirer.timer" and
                 * "acquirer.cTokenRegistration" these assignements will be visible to the
                 * timeout handler.
                 */
                if (timeout != Timeout.Infinite)
                    acquire.timer = new Timer(timeoutHandler, acquireNode, timeout, Timeout.Infinite);

                /**
                 * If the cancellation token is already in the canceled state, the cancellation
                 * handler will run immediately and synchronously, which *causes no damage* because
                 * this processing is terminal and the implicit locks can be acquired recursively.
                 */
                if (cToken.CanBeCanceled)
                    acquire.cTokenRegistration = cToken.Register(cancellationHandler, acquireNode);

                // Return the Task<bool> that represents the async acquire
                return acquire.Task;
            }
        }

        /**
	 * Wait until acquire multiple permits asynchronously enabling, optionally,
	 * a timeout and/or cancellation.
	 */
        public Task<bool> WaitAsync(int acquires = 1, int timeout = Timeout.Infinite,
            CancellationToken cToken = default(CancellationToken))
        {
            return AcquireAsync(acquires, timeout, cToken);
        }

        /**
	 * Releases the specified number of permits
	 */
        public void Release(int releases = 1)
        {
            // A list to hold temporarily the already satisfied asynchronous operations 
            List<AsyncAcquire> satisfied = null;
            lock (theLock)
            {
                // Validate argument
                if (permits + releases < permits || permits + releases > maxPermits)
                    throw new InvalidOperationException("Exceeded the maximum number of permits");
                permits += releases;
                // Satisfy the pending async acquires that the current value of permits allows.
                satisfied = SatisfyPendingAsyncAcquires();
            }

            // After release the lock, complete the tasks underlying all satisfied async acquires
            if (satisfied != null)
                CompleteSatisfiedAsyncAcquires(satisfied);
        }

        /**
	 *	Synchronous interface implemented using the asynchronous TAP interface.
	 */
        /**
         * Try to cancel an asynchronous acquire request identified by its task.
         *
         * Note: This method is needed to implement the synchronous interface.
         */
        private bool TryCancelAcquireAsyncByTask(Task<bool> acquireTask)
        {
            AsyncAcquire acquire = null;
            List<AsyncAcquire> satisfied = null;
            // To access the shared mutable state we must acquire the lock
            lock (theLock)
            {
                foreach (AsyncAcquire _acquire in asyncAcquires)
                {
                    if (_acquire.Task == acquireTask)
                    {
                        acquire = _acquire;
                        asyncAcquires.Remove(_acquire);
                        acquire.done = true;
                        if (asyncAcquires.Count > 0 && permits >= asyncAcquires.First.Value.acquires)
                            satisfied = SatisfyPendingAsyncAcquires();
                        break;
                    }
                }
            }

            // If we canceled the async acquire, process the cancellation
            if (acquire != null)
            {
                // After release the lock, complete any satisfied acquires
                if (satisfied != null)
                    CompleteSatisfiedAsyncAcquires(satisfied);

                // Dispose the resources associated with this async acquire and complete
                // its task to the Canceled state.
                acquire.Dispose();
                acquire.SetCanceled();
                return true;
            }

            return false;
        }

        /**
	 * Acquire one or multiple permits synchronously, enabling, optionally,
	 * a timeout and/or cancellation.
	 */
        public bool Acquire(int acquires = 1, int timeout = Timeout.Infinite,
            CancellationToken cToken = default(CancellationToken))
        {
            Task<bool> acquireTask = AcquireAsync(acquires, timeout, cToken);
            try
            {
                return acquireTask.Result;
            }
            catch (ThreadInterruptedException)
            {
                /**
                 * The acquirer thread was interrupted while waiting for task completion!
                 * Try to cancel the async acquire operation.
                 * Whether the cancellation was successful, throw interrupted exception.
                 */
                if (TryCancelAcquireAsyncByTask(acquireTask))
                    throw; // throw interrupted exception

                /**
                 * Here we known that the async acquire was already completed or cancelled.
                 * So we must return the underlying result, ignoring possible interrupts,
                 * while wait for task completion.
                 */
                try
                {
                    do
                    {
                        try
                        {
                            return acquireTask.Result;
                        }
                        catch (ThreadInterruptedException)
                        {
                            // ignore interrupts while waiting fro task's result
                        }
                        catch (AggregateException ae)
                        {
                            throw ae.InnerException;
                        }
                    } while (true);
                }
                finally
                {
                    // Anyway re-assert first interrupt on the current thead.
                    Thread.CurrentThread.Interrupt();
                }
            }
            catch (AggregateException ae)
            {
                // The acquire thrown an exception, propagate it synchronously
                throw ae.InnerException;
            }
        }

        /**
	 * Wait until acquire one or multiple permits synchronously, enabling, optionally,
	 * a timeout and/or cancellation.
	 */
        public bool Wait(int acquires = 1, int timeout = Timeout.Infinite,
            CancellationToken cToken = default(CancellationToken))
        {
            return Acquire(acquires, timeout, cToken);
        }

        /**
	 * Return the current number of available permits
	 */
        public int CurrentCount
        {
            get
            {
                lock (theLock) return permits;
            }
        }
    }
}