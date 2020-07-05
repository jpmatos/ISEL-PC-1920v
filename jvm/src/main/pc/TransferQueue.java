package pc;

import java.util.LinkedList;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

public class TransferQueue<E> {
    private final Lock mon = new ReentrantLock();
    private final Condition cond = mon.newCondition();
    private final LinkedList<Message> list = new LinkedList<>();

    public void put(E message){
        new Thread(() -> {
            try {
                mon.lock();

                //Add message to list
                Message msg = new Message(message, mon.newCondition());
                list.add(msg);
                cond.signal();
            } finally {
                mon.unlock();
            }
        }).start();
    }

    public boolean transfer(E message, long timeout) throws InterruptedException{
        try {
            mon.lock();

            //Add message to list
            Condition msgCond = mon.newCondition();
            Message msg = new Message(message, msgCond);
            list.add(msg);
            cond.signal();

            long limit = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(limit);
            while (true) {
                //Start wait
                msgCond.await(remaining, TimeUnit.MILLISECONDS);

                //See if message was taken
                if(msg.taken)
                    return true;

                //Leave wait loop if timeout reached
                remaining = Timeouts.remaining(limit);
                if (Timeouts.isTimeout(remaining)) {
                    list.remove(msg);
                    return false;
                }
            }
        } finally {
            mon.unlock();
        }
    }

    public E take(long timeout) throws InterruptedException{
        try {
            mon.lock();

            //Happy path
            if(!list.isEmpty()){
                Message msg = list.removeFirst();
                msg.taken = true;
                msg.cond.signal();
                return msg.message;
            }

            long limit = Timeouts.start(timeout);
            long remaining = Timeouts.remaining(limit);
            while (true) {
                //Start wait
                cond.await(remaining, TimeUnit.MILLISECONDS);

                //See if there's anything in the list
                if(!list.isEmpty()){
                    Message msg = list.removeFirst();
                    msg.taken = true;
                    msg.cond.signal();
                    return msg.message;
                }

                //Leave wait loop if timeout reached
                remaining = Timeouts.remaining(limit);
                if (Timeouts.isTimeout(remaining)) {
                    return null;
                }
            }
        } finally {
            mon.unlock();
        }
    }

    private class Message{
        private final E message;
        private final Condition cond;
        boolean taken = false;

        private Message(E message, Condition cond){
            this.message = message;
            this.cond = cond;
        }
    }
}
