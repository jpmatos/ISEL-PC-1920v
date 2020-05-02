package pt.isel.pc;

import java.util.HashMap;
import java.util.Optional;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

public class Exchanger<T> {
    private final Lock mon = new ReentrantLock();
    public final Condition cond = mon.newCondition();
    private DataPair pair = new DataPair();

    private class DataPair {
        private T firstData = null;
        private T secondData = null;
        boolean add(T data){
            if (firstData == null) {
                firstData = data;
                return false;
            } else {
                secondData = data;
                return true;
            }
        }
        void clearFirstData(){
            firstData = null;
        }
        void clearSecondData(){
            secondData = null;
        }
    }

    public Optional<T> exchange (T mydata, long timeout) throws InterruptedException{
        try {
            mon.lock();

            DataPair current = pair;

            //Already had data in there
            if (current.add(mydata) == true) {
                T resData = current.firstData;
                pair = new DataPair();
                cond.signal();
                return Optional.of(resData);
            }

            //No data in current
            long limit = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(limit);
            while (true) {
                //Start wait
                try {
                    cond.await(remaining, TimeUnit.MILLISECONDS);
                } catch (InterruptedException e) {
                    current.clearFirstData();
                    throw e;
                }

                //Check if another data was put in current
                if (current.secondData != null) {
                    T resData = current.secondData;
                    current.clearSecondData();
                    return Optional.of(resData);
                }

                //Leave if timeout reached
                remaining = Timeouts.remaining(limit);
                if (Timeouts.isTimeout(remaining)) {
                    current.clearFirstData();
                    return Optional.empty();
                }
            }
        } finally {
            mon.unlock();
        }
    }
}
