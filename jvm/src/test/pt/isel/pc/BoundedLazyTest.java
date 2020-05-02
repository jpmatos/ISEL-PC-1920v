package pt.isel.pc;


import org.junit.Test;

import java.util.LinkedList;
import java.util.List;
import java.util.Optional;
import java.util.function.Supplier;

public class BoundedLazyTest {

    @Test
    public void simpleBoundedLazyTest() throws InterruptedException {

        Supplier<Integer> supplier = () -> {
            try {
                Thread.sleep((long) (Math.random() * 10 * 1000));
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
            return (int) (Math.random() * 100);
        };

        BoundedLazy boundedLazy = new BoundedLazy(supplier, 4);
        List<Thread> ths = new LinkedList<>();

        for (int i = 0; i < 22; i++) {
            Thread th = new Thread(() -> {
                try {
                    Optional<Integer> res = boundedLazy.get((long) (Math.random() * 50 * 1000));
                    System.out.println(res);
                } catch (Exception e) {
                    assert false;
                }
            });
            th.start();
            ths.add(th);
        }
        for (Thread th : ths) {
            th.join();
        }
        assert true;
    }
}
