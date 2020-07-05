package pc;

import org.junit.Test;

import java.util.Collections;
import java.util.LinkedList;
import java.util.List;

public class SafeTransferQueueTest {
    static int num = 1;

    @Test
    public void simpleSafeTransferQueueTest() throws InterruptedException {
        SafeTransferQueue<String> transferQueue = new SafeTransferQueue<>();
        List<Thread> ths = new LinkedList<>();

        TakeMessage(transferQueue, null, ths);
        PutMessage(transferQueue, ths);
        PutMessage(transferQueue, ths);
        TakeMessage(transferQueue, "PutMessage 1", ths);
        PutMessage(transferQueue, ths);
        TakeMessage(transferQueue, "PutMessage 2", ths);
        TakeMessage(transferQueue, "PutMessage 3", ths);
        TakeMessage(transferQueue,null, ths);
        TakeMessage(transferQueue,null, ths);
        PutMessage(transferQueue, ths);
        TakeMessage(transferQueue, "PutMessage 4", ths);
        PutMessage(transferQueue, ths);
        TakeMessage(transferQueue, "PutMessage 5", ths);

        for (Thread th : ths) {
            th.join();
        }
    }

    @Test
    public void randomSafeTransferQueueTest() throws InterruptedException {
        SafeTransferQueue<String> transferQueue = new SafeTransferQueue<>();
        List<Thread> ths = new LinkedList<>();
        final int nOfThreads = 20;

        for (int i = 1; i < nOfThreads; i++) {
            int finalI = i;
            Thread th = new Thread(() -> transferQueue.put("PutMessage #" + finalI));
            ths.add(th);
        }

        for (int i = 1; i < nOfThreads - 3; i++) {
            int finalI = i;
            Thread th = new Thread(() -> {
                try {
                    Thread.sleep((long) (Math.random() * 2 * 1000));
                } catch (InterruptedException e) {
                    e.printStackTrace();
                }
                String res = transferQueue.take();
                if(res != null)
                    System.out.println("Take #" + finalI + " got message '" + res + "'");
                else
                    System.out.println("Take #" + finalI + " no message to take");
            });
            ths.add(th);
        }

        Collections.shuffle(ths);
        for (Thread th : ths) {
            th.start();
        }
        for (Thread th : ths) {
            th.join();
        }
        assert true;
    }

    private void TakeMessage(SafeTransferQueue<String> transferQueue, String expectedValue, List<Thread> ths) throws InterruptedException {
        Thread th = new Thread(() -> {
            String res = transferQueue.take();
            System.out.println(res);
            if(res == null)
                assert expectedValue == null;
            else
                assert res.equals(expectedValue);
        });
        th.start();
        ths.add(th);
        Thread.sleep(1000);
    }

    private void PutMessage(SafeTransferQueue<String> transferQueue, List<Thread> ths) throws InterruptedException {
        Thread th = new Thread(() -> transferQueue.put("PutMessage " + num++));
        th.start();
        ths.add(th);
        Thread.sleep(1000);
    }
}
