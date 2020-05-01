package pt.isel.pc;

import java.util.Optional;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

public class BroadcastBox<E> {
    private final Lock mon = new ReentrantLock();
    private final Condition cond = mon.newCondition();
    private Box currBox = new Box();


    public int deliverToAll(E message){
        Box seenBox = currBox;
        seenBox.message = message;
        new Thread(() -> {
            try {
                mon.lock();
                currBox = new Box();
                cond.signalAll();
            } finally {
                mon.unlock();
            }
        }).start();
        return seenBox.total;
    }

    public Optional<E> receive(long timeout) throws InterruptedException{
        try {
            mon.lock();
            Box enteredBox = currBox.enter();

            long limit = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(limit);
            while (true) {
                //Start wait
                cond.await(remaining, TimeUnit.MILLISECONDS);

                //See if there is a message in the box, and if this thread's place in the box wasn't taken
                if(enteredBox.message != null && enteredBox.taken < enteredBox.total)
                    return Optional.of(enteredBox.takeMessage());

                //Enter new box if thread's place was taken in previous box
                if(enteredBox.taken >= enteredBox.total)
                    enteredBox = currBox.enter();

                //Leave wait loop if timeout reached
                remaining = Timeouts.remaining(limit);
                if (Timeouts.isTimeout(remaining)) {
                    enteredBox.leave();
                    return Optional.empty();
                }
            }
        } finally {
            mon.unlock();
        }
    }

    private class Box{
        E message;
        int total = 0;
        int taken = 0;

        Box enter() {
            total++;
            return this;
        }

        void leave() {
            total--;
        }

        public E takeMessage() {
            taken++;
            return message;
        }
    }
}
