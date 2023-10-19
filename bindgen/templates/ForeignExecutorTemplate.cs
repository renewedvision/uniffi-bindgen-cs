class UniFfiForeignExecutorCallback {
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Delegate(IntPtr handle, uint delay, RustTask rustTask, IntPtr rustTaskData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void RustTask(IntPtr rustTaskData);

    // This cannot be a static method. Although C# supports implicitly using a static method as a
    // delegate, the behaviour is incorrect for this use case. Using static method as a delegate
    // argument creates an implicit delegate object, that is later going to be collected by GC. Any
    // attempt to invoke a garbage collected delegate results in an error:
    //   > A callback was made on a garbage collected delegate of type 'ForeignCallback::..'
    public static Delegate INSTANCE = (IntPtr handle, uint delayMs, RustTask rustTask, IntPtr rustTaskData) => {
        if (rustTask != null) {
            Task.Run(async () => {
                if (delayMs > 0) {
                    await Task.Delay(new TimeSpan(delayMs * TimeSpan.TicksPerMillisecond));
                }
                rustTask(rustTaskData);
            });
        }
    };
}

class FfiConverterForeignExecutor
{
    // Registers the foreign executor with the Rust side.
    public static void Register() {
        _UniFFILib.uniffi_foreign_executor_callback_set(UniFfiForeignExecutorCallback.INSTANCE);
    }
}
