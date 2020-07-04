using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComputeAsync
{
    public static class AsyncOperations
    {
        const int MIN_OPER_TIME = 100;
        const int MAX_OPER_TIME = 1000;
        private static int threshold = 50;


        private static void Print(string message)
        {
            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] {message}");
        }

        private static async Task<int> OperAsync(string argument, CancellationToken ctoken)
        {
            Random rnd = new Random();
            try
            {
                await Task.Delay(rnd.Next(MIN_OPER_TIME, MAX_OPER_TIME), ctoken);
                int next = rnd.Next(0, 100);
                Print("next: " + next);
                if (next >= threshold)
                {
                    Print("CommException raised.");
                    throw new CommException();
                }

                return argument.Length;
            }
            catch (OperationCanceledException)
            {
                Print("***delay canceled");
                throw;
            }
        }

        private static async Task<int> OperRetryAsync(string argument, int maxRetries, CancellationTokenSource lcts)
        {
            //Set Tasks in a List.
            List<Task<int>> operTasks = new List<Task<int>>();
            for (int i = 0; i < maxRetries; i++)
                operTasks.Add(OperAsync(argument, lcts.Token));

            while (true)
            {
                //Await for one of the tasks to complete.
                Task<int> completedTask = await Task.WhenAny(operTasks);
                try
                {
                    //Await for result. The task already completed, so either the result is there or there is
                    //an exception that will be thrown if tried to await for.
                    int res = await completedTask;
                    
                    //Before leaving, set an error handler for the rest of the tasks, so we observe the exceptions.
                    operTasks.Remove(completedTask);
                    foreach (Task<int> task in operTasks)
                        task.ContinueWith(err =>
                        {
                            if (err.Exception == null) 
                                return;
                            
                            //Observe Exception
                            foreach (Exception exception in err.Exception.Flatten().InnerExceptions)
                                Print("Observed Exception");
                        }, TaskContinuationOptions.OnlyOnFaulted);
                    
                    return res;
                }
                catch (OperationCanceledException)
                {
                    //Cancellation was requested, so return a cancelled task.
                    return await Task.FromCanceled<int>(lcts.Token);
                }
                catch (CommException)
                {
                    //A Comm exception occured, so remove it from the Task List.
                    operTasks.Remove(completedTask);
                    Print("CommException caught.");
                    
                    //If there are still tasks in the List, continue on the loop.
                    if (operTasks.Any()) 
                        continue;
                    
                    //If not, cancel the TokenSource and return a cancelled task.
                    lcts.Cancel();
                    return await Task.FromCanceled<int>(lcts.Token);
                }
            }
        }

        public static async Task<int[]> ComputeAsync(string[] elems, int maxRetries, CancellationToken ctoken)
        {
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ctoken);
            
            //Set Tasks in a List
            List<Task<int>> computeTasks = new List<Task<int>>();
            foreach (string elem in elems)
                computeTasks.Add(OperRetryAsync(elem, maxRetries, linkedCts));

            //Await for when all complete
            return await Task.WhenAll(computeTasks);
        }

        public static void SetThreshold(int newThreshold)
        {
            threshold = newThreshold;
        }
    }

    public class CommException : Exception
    {
        public CommException(string message = "communication error") : base(message)
        {
        }
    }
}