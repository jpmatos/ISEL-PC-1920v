package pc;

import java.util.LinkedList;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

public class SimpleThreadPoolExecutor {
    private final int keepAliveTime;
    private final int maxPoolSize;
    private int poolSize = 0;
    private final Lock mon = new ReentrantLock();
    private final Condition shuttingCond = mon.newCondition();
    private final LinkedList<Condition> conds = new LinkedList<>();
    private final LinkedList<WorkingThreadContainer> freeWorkingThreads = new LinkedList<>();
    private boolean isShuttingDown = false;
    private boolean isWaitingForShutdown = false;

    private class WorkingThreadContainer {
        Thread th;
        boolean hasNewCommand = false;
        boolean threadDone = true;
        Runnable command;
        private final Lock threadMon = new ReentrantLock();
        private final Condition threadCond = threadMon.newCondition();

        public WorkingThreadContainer(int keepAliveTime) {
            this.th = new Thread(() -> {
                try {
                    threadMon.lock();

                    //run the command this working thread was created for
                    command.run();
                    hasNewCommand = false;
                    boolean keepGoing = true;
                    while (keepGoing) {

                        //after it's done, put itself in the free list
                        addToFreeList();

                        //wait until timeout or a new command notifies it
                        try {
                            threadCond.await(keepAliveTime, TimeUnit.MILLISECONDS);
                        } catch (InterruptedException e) {
                            e.printStackTrace();
                        }

                        //if there's a new command, run it
                        if (hasNewCommand) {
                            hasNewCommand = false;
                            command.run();
                            //if there's not, proceed to end the thread
                        } else {
                            keepGoing = false;
                        }
                    }
                    threadDone = true;
                    decreasePool();
                }finally {
                    threadMon.unlock();
                }
            });
        }

        private void decreasePool() {
            try {
                mon.lock();
                poolSize--;
                //if there's a thread waiting for every working thread to finish, notify it telling this working thread has finished
                if(isWaitingForShutdown) {
                    shuttingCond.signal();
                }
            } finally {
                mon.unlock();
            }
        }

        private void addToFreeList() {
            try {
                mon.lock();
                freeWorkingThreads.add(this);
                //notify the first condition in the list that a new working thread was placed in the free threads list
                if (!conds.isEmpty())
                    conds.getFirst().signal();
            } finally {
                mon.unlock();
            }
        }

        public void addCommand(Runnable command){
            this.command = command;
            hasNewCommand = true;
        }

        public void start(){
            threadDone = false;
            th.start();
        }
    }

    public SimpleThreadPoolExecutor(int maxPoolSize, int keepAliveTime){
        this.maxPoolSize = maxPoolSize;
        this.keepAliveTime = keepAliveTime;
    }
    public boolean execute(Runnable command, int timeout) throws InterruptedException, RejectedExecutionException {
        Condition cond = null;
        try {
            mon.lock();
            cond = mon.newCondition();
            conds.add(cond);
            if(isShuttingDown){
                throw new RejectedExecutionException();
            }

            //happy path = at least one free thread
            if (freeWorkingThreads.isEmpty()){
                //if no free threads see if one can be created
                if(poolSize < maxPoolSize) {
                    freeWorkingThreads.add(new WorkingThreadContainer(keepAliveTime));

                    //if neither, go to wait
                } else {
                    try {
                        cond.await(timeout, TimeUnit.MILLISECONDS);
                    } catch (InterruptedException e){
                        throw e;
                    }
                }
            }
            //if no free threads are open even after waiting it out, return false
            if(freeWorkingThreads.isEmpty()) {
                return false;
            }

            //get the oldest free thread
            WorkingThreadContainer ths = freeWorkingThreads.getFirst();
            freeWorkingThreads.removeFirst();
            try {
                ths.threadMon.lock();
                //add this thread's command to the working thread
                ths.addCommand(command);

                //check if the working thread was just waiting or shut down
                if (ths.threadDone) {
                    ths.start();
                    poolSize++;
                } else {
                    ths.threadCond.signal();
                }
            } finally {
                ths.threadMon.unlock();
            }
            return true;
        } finally {
            conds.remove(cond);
            mon.unlock();
        }
    }
    public void Shutdown(){
        try{
            mon.lock();
            isShuttingDown = true;

            //notify all the waiting working threads so they can end
            for (WorkingThreadContainer th : freeWorkingThreads) {
                try {
                    th.threadMon.lock();
                    if(!th.threadDone)
                        th.threadCond.signal();
                } finally {
                    th.threadMon.unlock();
                }
            }
        } finally {
            mon.unlock();
        }
    }
    public boolean awaitTermination(int timeout) throws InterruptedException{
        try{
            mon.lock();
            long limit = Timeouts.start(timeout);
            while (poolSize != 0){
                long remaining = Timeouts.remaining(limit);
                isWaitingForShutdown = true;

                //wait until every working thread has ended (poolSize = 0)
                try {
                    shuttingCond.await(remaining, TimeUnit.MILLISECONDS);
                } catch (InterruptedException e){
                    throw e;
                }
                if(Timeouts.isTimeout(remaining)){
                    return false;
                }
            }
            return true;
        } finally {
            mon.unlock();
        }
    }

    //for testing purposes
    public int getPoolSize(){
        return poolSize;
    }
}
