package pc;

import java.util.LinkedList;
import java.util.Optional;
import java.util.concurrent.Callable;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

public class ThreadPoolExecutor<T> {
    private final int maxPoolSize;
    private final int keepAliveTime;
    private final Lock queueLock = new ReentrantLock();
    private final Lock threadLock = new ReentrantLock();
    private final Lock aliveLock = new ReentrantLock();
    private final Condition checkWorkThreadCond;
    private final Condition aliveThreadCond;
    private final LinkedList<Work> workQueue = new LinkedList<>();
    private final LinkedList<WorkingThread> waitingThreadQueue = new LinkedList<>();
    private final LinkedList<WorkingThread> aliveThreadQueue = new LinkedList<>();
    private boolean isShutdown = false;

    //Represents work to be executed or work already executed.
    private class Work implements Result {

        private final Callable<T> command;
        private boolean complete;
        private T result;
        private Exception exception;

        public Work(Callable<T> command) {
            this.command = command;
        }

        @Override
        public boolean isComplete() {
            return complete;
        }

        @Override
        public boolean tryCancel() {
            return false;
        }

        @Override
        public Optional get(int timeout) throws Exception {
            return Optional.of(result);
        }

        public void complete(T res) {
            complete = true;
            result = res;
        }

        public void exception(Exception e) {
            complete = true;
            this.exception = e;
        }
    }

    //Represents a Working Thread. This Thread executes it's given work then tries to get more work,
    //if there's not work it awaits, if it finishes await and there's still no work it terminates.
    private class WorkingThread {

        private Work work;
        private Condition threadCond;

        public WorkingThread(Work _work) {
            this.work = _work;
            this.threadCond = threadLock.newCondition();
            Thread thread = new Thread(() -> {
                boolean cont = true;
                while (cont) {

                    //Reset timeout
                    long limit = Timeouts.start(keepAliveTime);

                    //See if current work is not complete
                    if (!work.isComplete()) {

                        //Do work
                        try {
                            T res = work.command.call();
                            work.complete(res);
                        } catch (Exception e) {
                            e.printStackTrace();
                            work.exception(e);
                        }

                    }
                    else {
                        //If it is, try to get new work
                        try {
                            queueLock.lock();
                            if (workQueue.size() > 0) {
                                this.work = workQueue.removeFirst();
                                continue;
                            }
                        } finally {
                            queueLock.unlock();
                        }
                        //If it can't get new work, proceed to await process

                        //Terminate if timeout or Thread Pool is shutting down
                        long remaining = Timeouts.remaining(limit);
                        if (Timeouts.isTimeout(remaining) || isShutdown) {
                            cont = false;
                            continue;
                        }

                        //Start await
                        try {

                            //Add Thread to awaiting thread list
                            try {
                                threadLock.lock();
                                waitingThreadQueue.addLast(this);
                                threadCond.await(remaining, TimeUnit.MILLISECONDS);
                                waitingThreadQueue.remove(this);
                            } finally {
                                threadLock.unlock();
                            }
                        } catch (InterruptedException e) {
                            e.printStackTrace();
                            cont = false;

                            //Remove itself from awaiting thread list
                            try {
                                threadLock.lock();
                                waitingThreadQueue.remove(this);
                            } finally {
                                threadLock.unlock();
                            }
                        }

                        //See if new work was put in, if not then terminate
                        if (work.isComplete())
                            cont = false;
                    }
                }

                //Remove itself from alive thread list
                //Signal aliveThreadCond in case there's a thread waiting shutdown
                try {
                    aliveLock.lock();
                    aliveThreadQueue.remove(this);
                    if (aliveThreadQueue.size() == 0)
                        aliveThreadCond.signal();
                } finally {
                    aliveLock.unlock();
                }
            });

            thread.start();
        }

        public void assignWork(Work work) {
            this.work = work;
        }
    }

    //ThreadPool Builder
    public ThreadPoolExecutor(int _maxPoolSize, int keepAliveTime) {
        this.maxPoolSize = _maxPoolSize;
        this.keepAliveTime = keepAliveTime;
        this.checkWorkThreadCond = queueLock.newCondition();
        this.aliveThreadCond = aliveLock.newCondition();
        Thread checkWorkThread = new Thread(this::checkWorkThreadFunction);
        checkWorkThread.start();
    }

    //Give new work to be executed
    public Result<T> execute(Callable<T> command) {
        if (isShutdown)
            throw new RejectedExecutionException();

        try {
            queueLock.lock();
            Work work = new Work(command);
            workQueue.addLast(work);
            checkWorkThreadCond.signal();
            return work;
        } finally {
            queueLock.unlock();
        }
    }

    //Set to shutdown mode
    public void shutdown() {
        isShutdown = true;
    }

    //Await termination of every alive thread
    public boolean awaitTermination(int timeout) throws InterruptedException {
        try {
            aliveLock.lock();
            long limit = Timeouts.start(timeout);
            while (true) {
                if (aliveThreadQueue.size() == 0)
                    return true;
                else {
                    long remaining = Timeouts.remaining(limit);
                    if (Timeouts.isTimeout(remaining)) {
                        if (aliveThreadQueue.size() == 0)
                            return true;
                        else
                            return false;
                    }
                    aliveThreadCond.await(remaining, TimeUnit.MILLISECONDS);
                }
            }
        } finally {
            aliveLock.unlock();
        }
    }

    //Check Thread's implementation. This thread manages the working threads, waking them up if new work shows up,
    //and creates new working threads if possible.
    private void checkWorkThreadFunction() {
        try {
            queueLock.lock();
            while (true) {

                //See if there is work queued
                if (workQueue.size() > 0) {

                    //See if there's any thread awaiting
                    if (waitingThreadQueue.size() > 0) {
                        WorkingThread workingThread = waitingThreadQueue.removeFirst();
                        Work work = workQueue.removeFirst();

                        //When waking up a thread, assign it work beforehand, so it doesn't try to find work by itself
                        workingThread.assignWork(work);
                        workingThread.threadCond.signal();
                    }

                    //If there is no thread awaiting, see if it can create a new Working Thread
                    else if (aliveThreadQueue.size() < maxPoolSize) {
                        try {
                            aliveLock.lock();
                            Work work = workQueue.removeFirst();
                            WorkingThread workingThread = new WorkingThread(work);
                            aliveThreadQueue.addLast(workingThread);
                        } finally {
                            aliveLock.unlock();
                        }
                    }
                    //If it can not create a new Working Thread, go await
                    else {
                        try {
                            checkWorkThreadCond.await();
                        } catch (InterruptedException e) {
                            e.printStackTrace();
                        }
                    }
                //If no work is queued, go await
                } else {
                    try {
                        checkWorkThreadCond.await();
                    } catch (InterruptedException e) {
                        e.printStackTrace();
                    }
                }
            }
        } finally {
            queueLock.unlock();
        }
    }
}
