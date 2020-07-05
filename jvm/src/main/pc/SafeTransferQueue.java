package pc;

import java.util.concurrent.atomic.AtomicReference;

public class SafeTransferQueue<E> {
    Node<E> sentinel = new Node<E>(null);
    private final AtomicReference<Node<E>> head = new AtomicReference<>(sentinel);
    private final AtomicReference<Node<E>> tail = new AtomicReference<>(sentinel);

    public SafeTransferQueue() {
    }

    public void put(E message){
        Node<E> newTail = new Node<>(message);
        while (true) {
            Node<E> observedTail = tail.get();
            Node<E> observedTailNext = observedTail.next.get();
            if (observedTail == tail.get()) {	// confirm that we have a good tail, to prevent CAS failures
                if (observedTailNext != null) { /** step A **/
                    // queue in intermediate state, so advance tail for some other thread
                    boolean res = tail.compareAndSet(observedTail, observedTailNext);		/** step B **/
                } else {
                    // queue in quiescent state, try inserting new node
                    if (observedTail.next.compareAndSet(null, newTail)) {	/** step C **/
                        // advance the tail
                        boolean res2 = tail.compareAndSet(observedTail, newTail);	/** step D **/
                        break;
                    }
                }
            }
        }
    }

    public E take(){
        while(true){
            Node<E> observedHead = head.get();
            Node<E> newHead = observedHead.next.get();
            if(newHead != null){
                if(head.compareAndSet(observedHead, newHead)) {
                    return newHead.data;
                }
            } else {
                return null;
            }
        }
    }

    private static class Node<E> {
        final AtomicReference<Node<E>> next;
        final E data;

        Node(E data) {
            next = new AtomicReference<>(null);
            this.data = data;
        }
    }
}
