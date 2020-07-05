package pc;

import org.junit.Test;

import java.util.*;

import static junit.framework.TestCase.assertEquals;

public class ExchangeTest {

    @Test
    public void singleExchangeTest() throws InterruptedException {
        Exchanger exchanger = new Exchanger();
        int timeout = Integer.MAX_VALUE;

        final String[] th1Value = new String[1];
        final String[] th2Value = new String[1];
        th1Value[0] = "data1";
        th2Value[0] = "data2";

        Thread th1 = new Thread(() -> {
            try {
                Optional res = exchanger.exchange(th1Value[0], timeout);
                th1Value[0] = (String)res.get();
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        });

        Thread th2 = new Thread(() -> {
            try {
                Optional res = exchanger.exchange(th2Value[0], timeout);
                th2Value[0] = (String)res.get();
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        });


        th1.start();
        th2.start();

        th1.join();
        th2.join();

        assert th1Value[0].equals("data2");
        assert th2Value[0].equals("data1");
    }

    @Test
    public void multipleRandomExchangeTest() throws InterruptedException {
        Exchanger<String> exchanger = new Exchanger();
        List<Thread> ths = new LinkedList<>();
        int nOfThreads = 10;

        final String[] thData1 = new String[nOfThreads];
        final String[] thData1Exchanged = new String[nOfThreads];
        final String[] thData2Exchanged = new String[nOfThreads];


        for (int i = 0; i < nOfThreads; i++) {
            thData1[i] = "data"+i;
            int finalI = i;
            Thread th = new Thread(() ->{
                try {
                    Thread.sleep((long) (Math.random() * 15 * 1000 ));
                } catch (InterruptedException e) {
                    e.printStackTrace();
                }
                try {
                    Optional res = exchanger.exchange(thData1[finalI], (long) (Math.random() * 5 * 1000 ));
                    System.out.println(thData1[finalI] + " <-> " + (res.isPresent() ? res.get() : "TimedOut"));
                    thData1Exchanged[finalI] =  thData1[finalI];
                    thData2Exchanged[finalI] = res.isPresent() ? res.get().toString() : "TimedOut";
                } catch (InterruptedException e) {
                    e.printStackTrace();
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

        //Asserts
        for (int i = 0; i < nOfThreads; i++) {
            int index = Arrays.asList(thData1Exchanged).indexOf(thData2Exchanged[i]);
            if(index != -1)
                assert thData1Exchanged[i].equals(thData2Exchanged[index]);
        }
    }
}
