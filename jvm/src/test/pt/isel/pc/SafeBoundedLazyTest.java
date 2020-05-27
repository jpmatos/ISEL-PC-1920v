package pt.isel.pc;


import org.junit.Test;

import java.util.*;
import java.util.concurrent.atomic.AtomicIntegerArray;
import java.util.function.Supplier;

public class SafeBoundedLazyTest {

    @Test
    public void simpleSafeBoundedLazyTest() throws InterruptedException {

        final int MAX_VALUE_SIZE = 10000;
        final int MAX_RANDOM_WAIT_SECONDS = 2;
        final int TOTAL_LIVES = 5;
        final int TOTAL_THREADS = TOTAL_LIVES * 5;

        AtomicIntegerArray resArray = new AtomicIntegerArray(MAX_VALUE_SIZE);
         Supplier<Integer> supplier = () -> {
            try {
                Thread.sleep((long) (Math.random() * MAX_RANDOM_WAIT_SECONDS * 1000));
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
            int res = (int) (Math.random() * MAX_VALUE_SIZE);
            return res;
        };

        SafeBoundedLazy<Integer> boundedLazy = new SafeBoundedLazy<>(supplier, TOTAL_LIVES);
        List<Thread> ths = new LinkedList<>();

        for (int i = 0; i < TOTAL_THREADS; i++) {
            Thread th = new Thread(() -> {
                try {
                    Optional<Integer> res = boundedLazy.get();
                    resArray.addAndGet(res.get(), 1);
                } catch (Throwable e) {
                    assert false;
                }
            });
            th.start();
            ths.add(th);
        }

        for (Thread th : ths) {
            th.join();
        }

        for (int i = 0; i < resArray.length(); i++){
            assert (resArray.get(i) == TOTAL_LIVES || resArray.get(i) == 0);
        }
    }
}
