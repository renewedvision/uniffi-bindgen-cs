// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Threading;
using System;
using uniffi.objects;

namespace UniffiCS.BindingTests;

public class TestObjects
{
    [Fact]
    public void ObjectReferenceOutlivesInFlightCalls()
    {
        // This test ensures that objects aren't GC'ed while method calls are in-progress. From
        // logical perspective, it's entirely possible for `receiver` to be collected, even if
        // from the outside it looks like the method is being executed. This is because the compiler
        // only needs to touch the object before starting a method call, especially if the method
        // body invokes a native function that doesn't need to reference the object.

        var sender = StartReceiverThread();

        sender.WaitForReceiverToAppear();
        // receiver is in-flight

        CallGC();
        Assert.Equal(1, ObjectsMethods.GetLiveReceiverCount());

        // release receiver
        sender.SendSignal();
        sender.WaitForReceiverToDisappear();

        CallGC();
        Assert.Equal(0, ObjectsMethods.GetLiveReceiverCount());
    }

    // Separate scope to GC `receiver` on main thread, because simply nulling
    // local variable `receiver` does not let GC collect it.
    SignalSender StartReceiverThread() {
        (var sender, var receiver) = ObjectsMethods.CreateChannel();
        Thread thread = new Thread(
            new ThreadStart(() =>
            {
                // blocks until signal is sent
                receiver.ReceiveSignal();
            })
        );
        thread.Start();

        return sender;
    }

    static void CallGC() {
        GC.Collect();
        Thread.Sleep(10);
    }
}

