using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComputeAsync;
using NUnit.Framework;

namespace Tests
{
    public class Tests
    {
        [Test]
        public async Task SimpleComputeAsyncCompletionTest()
        {
            int maxTries = 3;
            string[] elems =
            {
                "elem1",
                "elem12",
                "elem123",
                "elem"
            };
            int[] expected =
            {
                5,
                6,
                7,
                4
            };
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            AsyncOperations.SetThreshold(95); //Set a high threshold so tasks will unlikely fault.
            try
            {
                int[] res = await AsyncOperations.ComputeAsync(elems, maxTries, tokenSource.Token);
                Assert.IsTrue(res.SequenceEqual(expected));
            }
            catch (TaskCanceledException)
            {
                Assert.Fail();
            }
        }

        [Test]
        public async Task SimpleComputeAsyncFaultedTest()
        {
            int maxTries = 3;
            string[] elems =
            {
                "elem1",
                "elem12",
                "elem123",
                "elem"
            };
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            AsyncOperations.SetThreshold(5); //Set a low threshold so tasks will likely fault and then cancel.
            try
            {
                await AsyncOperations.ComputeAsync(elems, maxTries, tokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                Assert.Pass();
            }

            Assert.Fail();
        }

        [Test]
        public async Task SimpleComputeAsyncCanceledTest()
        {
            int maxTries = 3;
            string[] elems =
            {
                "elem1",
                "elem12",
                "elem123",
                "elem"
            };
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            AsyncOperations.SetThreshold(95); //Set a high threshold so the Task is cancelled by the token.
            try
            {
                Task<int[]> task = AsyncOperations.ComputeAsync(elems, maxTries, tokenSource.Token);

                //Wait a while then Cancel
                await Task.Delay(100);
                tokenSource.Cancel();

                //Wait for Cancelled task, which will throw the expected exception.
                await task;
            }
            catch (TaskCanceledException)
            {
                Assert.Pass();
            }

            Assert.Fail();
        }
    }
}