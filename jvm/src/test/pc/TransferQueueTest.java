package pc;

import org.junit.Test;

import java.util.Collections;
import java.util.LinkedList;
import java.util.List;

public class TransferQueueTest {

    @Test
    public void simpleTransferQueueTest() throws InterruptedException {
        TransferQueue<String> transferQueue = new TransferQueue();

        Thread th1 = newTransferThread(transferQueue, "TransferMessage #1");
        th1.start();
        Thread.sleep(1000);

        Thread th2 = new Thread(() -> transferQueue.put("PutMessage #2"));
        th2.start();
        Thread.sleep(1000);

        Thread th3 = newTransferThread(transferQueue, "TransferMessage #3");
        th3.start();
        Thread.sleep(1000);

        Thread th4 = newTransferTakeThread(transferQueue, "TransferMessage #1");
        th4.start();
        Thread.sleep(1000);

        Thread th5 = newTransferTakeThread(transferQueue, "PutMessage #2");
        th5.start();
        Thread.sleep(1000);

        Thread th6 = newTransferTakeThread(transferQueue, "TransferMessage #3");
        th6.start();
        Thread.sleep(1000);

        th1.join();
        th2.join();
        th3.join();
        th4.join();
        th5.join();
        th6.join();
    }

    @Test
    public void randomTransferQueueTest() throws InterruptedException {
        TransferQueue<String> transferQueue = new TransferQueue();
        List<Thread> ths = new LinkedList<>();
        final int nOfThreads = 20;

        for (int i = 1; i < nOfThreads; i++) {
            int finalI = i;
            Runnable runnable;
            if(Math.random() * 10 > 3 && i != nOfThreads - 1){
                runnable = () -> {
                    try {
                        boolean res = transferQueue.transfer("TransferMessage #" + finalI, (long) (Math.random() * 20 * 1000 + 5));
                        if(res)
                            System.out.println("TransferMessage #" + finalI + " received by another thread");
                        else
                            System.out.println("TransferMessage #" + finalI + " timed out");
                    } catch (InterruptedException e) {
                        assert false;
                    }
                };
            } else {
                runnable = () -> transferQueue.put("PutMessage #" + finalI);
            }
            Thread th = new Thread(runnable);
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
                try {
                    String res = transferQueue.take((long) (Math.random() * 20 * 1000 + 5));
                    if(res != null)
                        System.out.println("Take #" + finalI + " got message '" + res + "'");
                    else
                        System.out.println("Take #" + finalI + " timed out");
                } catch (InterruptedException e) {
                    assert false;
                }
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

    private Thread newTransferThread(TransferQueue<String> transferQueue, String str) {
        return new Thread(() -> {
            try {
                boolean res = transferQueue.transfer(str, 5 * 1000);
                if(!res)
                    assert false;
            } catch (InterruptedException e) {
                assert false;
            }
        });
    }

    private Thread newTransferTakeThread(TransferQueue<String> transferQueue, String str){
        return new Thread(() -> {
            try {
                String res = transferQueue.take(5 * 1000);
                assert res.equals(str);
            } catch (InterruptedException e) {
                assert false;
            }
        });
    }
}
