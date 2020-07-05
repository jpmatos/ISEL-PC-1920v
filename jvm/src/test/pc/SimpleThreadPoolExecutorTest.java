package pc;

import org.junit.Test;
import org.junit.platform.commons.logging.Logger;
import org.junit.platform.commons.logging.LoggerFactory;

import java.util.concurrent.RejectedExecutionException;

import static junit.framework.TestCase.assertEquals;
import static junit.framework.TestCase.assertTrue;
import static org.junit.Assert.assertFalse;

public class SimpleThreadPoolExecutorTest {
    private static Logger logger = LoggerFactory.getLogger(SimpleThreadPoolExecutor.class);

    @Test
    public void singleThreadTest() throws InterruptedException {
        SimpleThreadPoolExecutor executor = new SimpleThreadPoolExecutor(3, 10000);
        boolean ret = executor.execute(() -> System.out.println("task1"), 1000);
        assertTrue(ret);
    }

    @Test
    public void singleThreadShutdownTest() throws InterruptedException {
        SimpleThreadPoolExecutor executor = new SimpleThreadPoolExecutor(3, 8000);
        executor.execute(() -> System.out.println("task1"), 1000);
        Thread.sleep(1000); //required
        executor.Shutdown();

        boolean rejected = false;
        try {
            executor.execute(() -> System.out.println("task2"), 1000);
        } catch (RejectedExecutionException e){
            rejected=true;
        }

        boolean ret = executor.awaitTermination(20000);
        assertTrue(ret);
        assertTrue(rejected);
    }

    @Test
    public void everyThreadFullTest() throws InterruptedException {
        SimpleThreadPoolExecutor executor = new SimpleThreadPoolExecutor(3, 8000);
        executor.execute(() -> {
            System.out.println("task1: sleep5000");
            try {
                Thread.sleep(5000);
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        }, 1000);
        executor.execute(() -> {
            System.out.println("task2: sleep5000");
            try {
                Thread.sleep(5000);
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        }, 1000);
        executor.execute(() -> {
            System.out.println("task3: sleep5000");
            try {
                Thread.sleep(5000);
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        }, 1000);

        boolean ret = executor.execute(() -> System.out.println("task4"), 1000);
        assertFalse(ret);
    }

    @Test
    public void reuseThreadsTest() throws InterruptedException {
        SimpleThreadPoolExecutor executor = new SimpleThreadPoolExecutor(3, 8000);
        final long[] task1ID = new long[1];
        final long[] task2ID = new long[1];
        final long[] task3ID = new long[1];
        executor.execute(() -> {
            task1ID[0] = Thread.currentThread().getId();
            System.out.println("Thread Id-" + task1ID[0]  + ": task1 sleep5000");
            try {
                Thread.sleep(5000);
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        }, 1000);
        executor.execute(() -> {
            task2ID[0] = Thread.currentThread().getId();
            System.out.println("Thread Id-" + task2ID[0] + ": task2 sleep10000");
            try {
                Thread.sleep(10000);
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        }, 1000);
        assertEquals(2, executor.getPoolSize());

        logger.info(() ->"Waiting for task1 to finish.");
        Thread.sleep(5000);

        executor.execute(() -> {
            task3ID[0] = Thread.currentThread().getId();
            System.out.println("Thread Id-" + task3ID[0] + ": task3 nosleep");

        }, 1000); //should reuse 1st thread

        Thread.sleep(1000); //required to wait for task3 to start before evaluating. Should be replaced by .join() somehow.
        logger.info(() -> "task3 should have the same id as task1");
        assertEquals(task1ID[0], task3ID[0]);
    }
}
