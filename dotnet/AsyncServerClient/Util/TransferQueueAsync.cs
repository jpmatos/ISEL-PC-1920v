using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncServerClient.Util
{
    public class TransferQueueAsync<T> where T : class
    {
        // Base type for the two async requests
        private class AsyncRequest<V> : TaskCompletionSource<V>
        {
            internal readonly CancellationToken cToken; // cancellation token
            internal CancellationTokenRegistration cTokenRegistration; // used to dispose the cancellation handler 
            internal Timer timer;
            internal bool done; // true when the async request is completed or canceled

            // same as AsyncAcquire on the SemaphoreAsync
            internal AsyncRequest(CancellationToken cToken)
            {
                this.cToken = cToken;
            }

            internal void Dispose(bool canceling = false)
            {
                if (!canceling && cToken.CanBeCanceled)
                    cTokenRegistration.Dispose();
                timer?.Dispose();
            }
        }

        // Type used by async transfer take request
        private class AsyncTransfer : AsyncRequest<bool>
        {
            internal AsyncTransfer(CancellationToken cToken) : base(cToken)
            {
            }
        }

        // Type used by async take requests
        private class AsyncTake : AsyncRequest<T>
        {
            internal AsyncTake(CancellationToken cToken) : base(cToken)
            {
            }
        }

        class Message
        {
            internal readonly T message;
            internal readonly AsyncTransfer transfer;

            internal Message(T message, AsyncTransfer transfer = null)
            {
                this.message = message;
                this.transfer = transfer;
            }
        }

        private readonly object theLock = new object();
        private readonly LinkedList<Message> pendingMessage;
        private readonly LinkedList<AsyncTake> asyncTakes;
        private readonly Action<object> cancellationHandlerTransfer;
        private readonly TimerCallback timeoutHandlerTransfer;
        private readonly Action<object> cancellationHandlerTake;
        private readonly TimerCallback timeoutHandlerTake;
        private CancellationToken parentCToken;

        public TransferQueueAsync(CancellationToken cToken)
        {
            parentCToken = cToken;
            pendingMessage = new LinkedList<Message>();
            asyncTakes = new LinkedList<AsyncTake>();
            cancellationHandlerTransfer = new Action<object>((acquireNode) =>
                CancellationHandler(acquireNode, true, true));
            timeoutHandlerTransfer = new TimerCallback((acquireNode) =>
                CancellationHandler(acquireNode, false, true));
            cancellationHandlerTake = new Action<object>((acquireNode) =>
                CancellationHandler(acquireNode, true, false));
            timeoutHandlerTake = new TimerCallback((acquireNode) =>
                CancellationHandler(acquireNode, false, false));
        }

        public void Put(T payload)
        {
            lock (theLock)
            {
                if (asyncTakes.Any())
                {
                    AsyncTake asyncTake = asyncTakes.First();
                    asyncTakes.RemoveFirst();
                    asyncTake.done = true;
                    asyncTake.SetResult(payload);
                    return;
                }

                Message message = new Message(payload);
                pendingMessage.AddLast(message);
            }
        }

        public Task<bool> Transfer(T payload, int timeout = Timeout.Infinite)
        {
            lock (theLock)
            {
                //Create CTS from the Server's token so when Server cancels every Transfer cancels too
                CancellationToken cToken = CancellationTokenSource.CreateLinkedTokenSource(parentCToken).Token;
                AsyncTransfer asyncTransfer = new AsyncTransfer(cToken);
                if (asyncTakes.Any())
                {
                    AsyncTake asyncTake = asyncTakes.First();
                    asyncTakes.RemoveFirst();
                    asyncTake.done = true;
                    asyncTransfer.done = true;
                    asyncTake.SetResult(payload);
                    asyncTransfer.SetResult(true);
                    return asyncTransfer.Task;
                }

                Message message = new Message(payload, asyncTransfer);
                LinkedListNode<Message> node = pendingMessage.AddLast(message);

                if (timeout != Timeout.Infinite)
                    asyncTransfer.timer = new Timer(timeoutHandlerTransfer, node, timeout, Timeout.Infinite);

                if (cToken.CanBeCanceled)
                    asyncTransfer.cTokenRegistration = cToken.Register(cancellationHandlerTransfer, node);

                return asyncTransfer.Task;
            }
        }

        public Task<T> Take(int timeout = Timeout.Infinite)
        {
            lock (theLock)
            {
                //Create CTS from the Server's token so when Server cancels every Take cancels too
                CancellationToken cToken = CancellationTokenSource.CreateLinkedTokenSource(parentCToken).Token;
                AsyncTake asyncTake = new AsyncTake(cToken);
                if (pendingMessage.Any())
                {
                    Message message = pendingMessage.First();
                    pendingMessage.RemoveFirst();
                    if (message.transfer != null)
                    {
                        message.transfer.done = true;
                        message.transfer.SetResult(true);
                    }

                    asyncTake.done = true;
                    asyncTake.SetResult(message.message);
                    return asyncTake.Task;
                }

                LinkedListNode<AsyncTake> node = asyncTakes.AddLast(asyncTake);

                if (timeout != Timeout.Infinite)
                    asyncTake.timer = new Timer(timeoutHandlerTake, node, timeout, Timeout.Infinite);

                if (cToken.CanBeCanceled)
                    asyncTake.cTokenRegistration = cToken.Register(cancellationHandlerTake, node);

                return asyncTake.Task;
            }
        }

        private void CancellationHandler(object _acquireNode, bool canceling, bool transfer)
        {
            if (transfer)
            {
                LinkedListNode<Message> acquireNode = (LinkedListNode<Message>) _acquireNode;
                Message acquire = acquireNode.Value;

                bool complete = false;
                lock (theLock)
                {
                    if (!acquire.transfer.done)
                    {
                        pendingMessage.Remove(acquireNode);
                        acquire.transfer.done = true;
                        complete = true;
                        // Console.WriteLine("Removed from pendingMessage list.");
                    }
                }

                if (complete)
                {
                    if (canceling)
                        acquire.transfer.SetCanceled();
                    else
                        acquire.transfer.SetResult(false);
                }
            }
            else
            {
                LinkedListNode<AsyncTake> acquiredNode = (LinkedListNode<AsyncTake>) _acquireNode;
                AsyncTake acquire = acquiredNode.Value;

                bool complete = false;
                lock (theLock)
                {
                    if (!acquire.done)
                    {
                        asyncTakes.Remove(acquiredNode);
                        acquire.done = true;
                        complete = true;
                        // Console.WriteLine("Removed from asyncTakes list.");
                    }
                }

                if (complete)
                {
                    if (canceling)
                        acquire.SetCanceled();
                    else
                        acquire.SetResult(null);
                }
            }
        }
    }
}