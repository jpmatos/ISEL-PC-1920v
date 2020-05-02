package pt.isel.pc;

import org.junit.Test;

import java.util.Collections;
import java.util.LinkedList;
import java.util.List;
import java.util.Optional;

public class BroadcastBoxTest {

    @Test
    public void simpleBroadcastBoxTest() throws InterruptedException {
        BroadcastBox<String> broadcastBox = new BroadcastBox();

        for (int j = 0; j < 10; j++) {
            List<Thread> ths = new LinkedList<>();
            final int[] received = {0};
            for (int i = 0; i < 10; i++) {
                Thread th = new Thread(() -> {
                    try {
                        Optional msg = broadcastBox.receive((long) (Math.random() * 10 * 1000 ));
                        if(msg != Optional.empty())
                            received[0]++;
                        System.out.println(msg);
                    } catch (InterruptedException e) {
                        assert false;
                    }
                });
                th.start();
                ths.add(th);
            }
            Thread.sleep(5 * 1000);

            final int[] totalDelivered = {0};
            Thread thDel = new Thread(() -> {
                int total = broadcastBox.deliverToAll("Message #" + (int)(Math.random() * 100));
                totalDelivered[0] = total;
                System.out.println("Message delivered to [" + total + "] threads");
            });
            thDel.start();
            ths.add(thDel);

            for (Thread th : ths) {
                th.join();
            }
            assert totalDelivered[0] == received[0];
        }
    }

    @Test
    public void randomBroadcastBoxTest() throws InterruptedException {
        BroadcastBox<String> broadcastBox = new BroadcastBox();
        List<Thread> ths = new LinkedList<>();
        final int nOfThreads = 50;

        for (int i = 0; i < nOfThreads; i++) {
            Thread.sleep((long) (Math.random() * 2 * 1000));
            Runnable runnable;
            if(Math.random() * 10 > 3 && i != nOfThreads - 1) {
                runnable = () -> {
                    try {
                        Optional msg = broadcastBox.receive((long) (Math.random() * 20 * 1000 + 5));
                        System.out.println(msg);
                    } catch (InterruptedException e) {
                        assert false;
                    }
                };
            }
            else {
                runnable = () -> {
                    int total = broadcastBox.deliverToAll("Message #" + (int)(Math.random() * 100));
                    System.out.println("Message delivered to [" + total + "] threads");
                };
            }
            Thread th = new Thread(runnable);
            th.start();
            ths.add(th);
        }

        for (Thread th : ths) {
            th.join();
        }
        assert true;
    }
}
