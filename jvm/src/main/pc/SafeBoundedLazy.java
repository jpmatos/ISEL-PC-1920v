package pc;

import java.util.Optional;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Supplier;

class SafeBoundedLazy<E> {
    private static class ValueHolder<V> {
        V value;
        AtomicInteger availableLives;

        ValueHolder(V value, int lives) {
            this.value = value;
            availableLives =  new AtomicInteger(lives);
        }

        ValueHolder() {
        }
    }

    // Configuration arguments
    private final Supplier<E> supplier;
    private final int lives;
    /**
     * The possible states:
     * <p>
     * null: means UNCREATED
     * <p>
     * CREATING and ERROR: mean exactly that
     * <p>
     * != null && != ERROR && != CREATING: means CREATED
     */
    private final AtomicReference<ValueHolder<E>> ERROR = new AtomicReference<>();
    private final AtomicReference<ValueHolder<E>> CREATING = new AtomicReference<>(new ValueHolder<>());
    // The current state
    private AtomicReference<ValueHolder<E>> state = new AtomicReference<>(null);
    // When the synchronizer is in ERROR state, the exception is hold here
    private Throwable errorException;

    // Construct a BoundedLazy
    public SafeBoundedLazy(Supplier<E> supplier, int lives) {
        if (lives < 1)
            throw new IllegalArgumentException();
        this.supplier = supplier;
        this.lives = lives;
    } // Returns an instance of the underlying type

    public Optional<E> get() throws Throwable {
        System.out.println(Thread.currentThread().getId() +" - New");
        while(true){
            ValueHolder<E> observedState = state.get();
            if (state == ERROR) {
                //Area 0. - State is ERROR
                System.out.println(Thread.currentThread().getId() +" - Enter Area 0");
                throw errorException;
            }

            if(observedState == null){
                // Area 1. - Trying to gain CREATING
                System.out.println(Thread.currentThread().getId() +" - Enter Area 1");
                if(state.compareAndSet(null, CREATING.get())){
                    System.out.println(Thread.currentThread().getId() +" - Set CREATE");
                    // Only one thread can be inside this block at once (The thread that set State to CREATING)
                    // If there is one thread in here, it means no new threads can reach Area 3.
                    try {
                        E value = supplier.get();
                        state.set(new ValueHolder<>(value, lives - 1));
                        System.out.println(Thread.currentThread().getId() +" - Set new value: " + value);
                        return Optional.of(value);
                    } catch (Throwable ex) {
                        System.out.println(Thread.currentThread().getId() +" - ERROR occured when CREATING");
                        errorException = ex;
                        state = ERROR;
                        throw ex;
                    }
                }
                System.out.println(Thread.currentThread().getId() +" - Failed set CREATE");
            }
            else if (observedState == CREATING.get()) {
                // Area 2. - Waiting for CREATING to finish (successfully or not)
                System.out.println(Thread.currentThread().getId() +" - Enter Area 2");
                do {
//                    System.out.println(Thread.currentThread().getId() +" - Wielding...");
                    Thread.yield();
                } while (state.get() == CREATING.get());
                System.out.println(Thread.currentThread().getId() +" - Wield over");
            } else {
                // Area 3. - Trying to obtain a live from the observed value
                System.out.println(Thread.currentThread().getId() +" - Enter Area 3");
                int observedLives = observedState.availableLives.get();
                int newLives = observedLives - 1;
                if(newLives < 0) {
                    System.out.println(Thread.currentThread().getId() +" - No more lives");
                    continue;
                }
                if(observedState.availableLives.compareAndSet(observedLives, newLives)){
                    if(newLives == 0) {
                        System.out.println(Thread.currentThread().getId() +" - Got last live. Set state null");
                        state = new AtomicReference<>(null);
                    }
                    System.out.println(Thread.currentThread().getId() +" - Returning value: -------------" + observedState.value);
                    return Optional.of(observedState.value);
                }
                System.out.println(Thread.currentThread().getId() +" - Failed acquire live");
            }
        }
    }
}