{#/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */#}

// `SafeHandle` implements the semantics outlined below, i.e. its thread safe, and the dispose
// method will only be called once, once all outstanding native calls have completed.
// https://github.com/mozilla/uniffi-rs/blob/0dc031132d9493ca812c3af6e7dd60ad2ea95bf0/uniffi_bindgen/src/bindings/kotlin/templates/ObjectRuntime.kt#L31
// https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.criticalhandle
//
// SafeHandle is used to manage ownership of the Rust object. When SafeHandle goes out of scope,
// the underlying Rust reference is decremented. Method calls and lowering increment ("clone")
// object reference, and Rust is responsible for decrementing the reference. This means, it's not
// necessary to use SafeHandle directly for method calls and lowering, since Rust will take care
// of decrementing the reference.
//
// However, it's still necessary to use SafeHandle for "cloning" the reference, since CG may choose
// to collect the object itself and run the finalizer, in turn decrementing Rust reference and
// causing use-after-free during "clone" native call. (See SafeHandle documentation).

{{ config.access_modifier() }} abstract class FFIObject<THandle>: IDisposable where THandle : FFISafeHandle {
    private THandle handle;

    public FFIObject(THandle handle) {
        this.handle = handle;
    }

    public THandle GetHandle() {
        return handle;
    }

    public void Dispose() {
        handle.Dispose();
    }
}

{{ config.access_modifier() }} abstract class FFISafeHandle: SafeHandle {
    public FFISafeHandle(): base(new IntPtr(0), true) {
    }

    public FFISafeHandle(IntPtr pointer): this() {
        this.SetHandle(pointer);
    }

    public override bool IsInvalid {
        get {
            return handle.ToInt64() == 0;
        }
    }
}

static class FFIObjectUtil {
    public static void DisposeAll(params Object?[] list) {
        foreach (var obj in list) {
            Dispose(obj);
        }
    }

    // Dispose is implemented by recursive type inspection at runtime. This is because
    // generating correct Dispose calls for recursive complex types, e.g. List<List<int>>
    // is quite cumbersome.
    private static void Dispose(dynamic? obj) {
        if (obj == null) {
            return;
        }

        if (obj is IDisposable disposable) {
            disposable.Dispose();
            return;
        }

        var type = obj.GetType();
        if (type != null) {
            if (type.IsGenericType) {
                if (type.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>))) {
                    foreach (var value in obj) {
                        Dispose(value);
                    }
                } else if (type.GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>))) {
                    foreach (var value in obj.Values) {
                        Dispose(value);
                    }
                }
            }
        }
    }
}
