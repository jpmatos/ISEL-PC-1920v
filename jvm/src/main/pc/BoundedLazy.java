package pc;

import java.util.Optional;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;
import java.util.function.Supplier;

public class BoundedLazy<E> {
    private Supplier<E> supplier;
    private int lives;
    private int maxLives;

    private E res = null;
    private boolean isCalculating = false;
    private boolean isException = false;
    private Exception exception = null;

    private final Lock mon = new ReentrantLock();
    private final Condition cond = mon.newCondition();

    public BoundedLazy(Supplier<E> supplier, int lives){
        this.supplier = supplier;
        this.lives = lives;
        this.maxLives = lives;
    }

    public Optional<E> get(long timeout) throws Exception {
        try {
            mon.lock();

            //Check if there is an exception
            if(isException)
                throw exception;

            //Happy path
            if(res != null && lives > 0){
                lives--;
                return Optional.of(res);
            }

            //See if current value is being calculated
            if(isCalculating){
                long limit = Timeouts.start(timeout);
                long remaining = Timeouts.remaining(limit);
                while (true) {
                    //Start wait
                    cond.await(remaining, TimeUnit.MILLISECONDS);

                    //Check if there was an exception
                    if(isException)
                        throw exception;

                    //Check if value was calculated and lives remain
                    if(res != null && lives > 0){
                        lives--;
                        return Optional.of(res);
                    }

                    //See if it being calculated again. If not, leave loop and have this thread do it
                    if(!isCalculating){
                        isCalculating = true;
                        break;
                    }

                    //Leave wait loop if timeout reached
                    remaining = Timeouts.remaining(limit);
                    if (Timeouts.isTimeout(remaining)) {
                        return Optional.empty();
                    }
                }
            }
            //If not, this thread will do it
            else {
                isCalculating = true;
                }
            } finally {
                mon.unlock();
            }

        //Start calculating value
        try {
            res = supplier.get();

            lives = maxLives - 1;
            isCalculating = false;
            return Optional.of(res);
        }
        catch (Exception e){
            isCalculating = false;
            isException = true;
            exception = e;
            throw e;
        }
        finally {
            try {
                mon.lock();
                cond.signalAll();
            } finally {
                mon.unlock();
            }
        }
    }
}
