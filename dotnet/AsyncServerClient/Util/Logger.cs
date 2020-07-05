using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncServerClient.Util
{
    public class Logger
    {
        private ConcurrentQueue<string> queue;
        private int maxLog;
        private bool finish;

        public Logger(int maxLog)
        {
            queue = new ConcurrentQueue<string>();
            this.maxLog = maxLog;
            finish = false;
        }

        public void Init()
        {
            Thread logger = new Thread(() =>
            {
                while (!finish)
                {
                    // await Task.Delay(1000);
                    if (queue.TryDequeue(out string message))
                        Console.WriteLine($"Log - {message}");
                }
            });
            logger.Priority = ThreadPriority.Lowest;
            logger.Start();
        }

        public void Log(string message)
        {
            if (queue.Count < maxLog)
            {
                queue.Enqueue(message);
            }
        }

        public Task FinishLogs()
        {
            finish = true;
            Task task = new Task(() =>
            {
                while (!queue.IsEmpty)
                {
                    if (queue.TryDequeue(out string message))
                        Console.WriteLine($"Log - {message}");
                }
            });
            task.Start();
            return task;
        }

        // public void Put(string message)
        // {
        //     Node newTail = new Node(message);
        //     while (true)
        //     {
        //         Node observedTail = tail;
        //         Node observedTailNext = tail.next;
        //         if (observedTail == tail)
        //         {
        //             if (observedTailNext != null)
        //                 Interlocked.CompareExchange(ref tail, observedTailNext, observedTail);
        //             else
        //             {
        //                 if (Interlocked.CompareExchange(ref observedTail.next, newTail, null) == newTail)
        //                 {
        //                     Interlocked.CompareExchange(ref tail, newTail, observedTail);
        //                     break;
        //                 }
        //             }
        //         }
        //     }
        // }
        //
        // private class Node {
        //     internal Node next;
        //     string data;
        //
        //     internal Node(string data)
        //     {
        //         next = null;
        //         this.data = data;
        //     }
        // }
    }
}