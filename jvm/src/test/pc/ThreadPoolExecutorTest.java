package pc;

import org.junit.Assert;
import org.junit.Test;

public class ThreadPoolExecutorTest {

    @Test
    public void singleThreadTest() throws Exception {
        ThreadPoolExecutor executor = new ThreadPoolExecutor(3, 10000);
        Result result = executor.execute(() -> {
            Thread.sleep(2000);
            return true;
        });
        Thread.sleep(5000);
        Assert.assertTrue(result.isComplete());
    }

    @Test
    public void everyThreadFullTest() throws InterruptedException {
        ThreadPoolExecutor executor = new ThreadPoolExecutor(3, 10000);
        Result result1 = executor.execute(() -> {
            Thread.sleep(5000);
            return true;
        });
        Result result2 = executor.execute(() -> {
            Thread.sleep(5000);
            return true;
        });
        Result result3 = executor.execute(() -> {
            Thread.sleep(5000);
            return true;
        });
        Result result4 = executor.execute(() -> {
            Thread.sleep(5000);
            return true;
        });
        Result result5 = executor.execute(() -> {
            Thread.sleep(5000);
            return true;
        });
        Thread.sleep(6000);
        Assert.assertTrue(result1.isComplete());
        Assert.assertTrue(result2.isComplete());
        Assert.assertTrue(result3.isComplete());
        Assert.assertFalse(result4.isComplete());
        Thread.sleep(12000);
        Assert.assertTrue(result5.isComplete());
    }

    @Test
    public void awaitShutdownTest() throws InterruptedException {
        ThreadPoolExecutor executor = new ThreadPoolExecutor(3, 10000);
        Result result1 = executor.execute(() -> {
            Thread.sleep(5000);
            return true;
        });
        Result result2 = executor.execute(() -> {
            Thread.sleep(5000);
            return true;
        });
        Result result3 = executor.execute(() -> {
            Thread.sleep(5000);
            return true;
        });

        Thread.sleep(1000);
        executor.shutdown();
        Assert.assertTrue(executor.awaitTermination(6000));
    }
}
