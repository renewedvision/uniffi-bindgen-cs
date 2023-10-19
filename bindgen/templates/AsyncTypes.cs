// Async return type handlers
{{- self.add_import("System.Threading.Tasks") }}

// FFI type for callback handlers
{%- for callback_param in ci.iter_future_callback_params()|unique_ffi_types %}
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void UniFfiFutureCallback{{ callback_param|ffi_type_name }}(IntPtr callbackData, {{ callback_param|ffi_type_name }} returnValue, RustCallStatus callStatus);
{% endfor %}

// Callback handlers for an async call. These are invoked by Rust when the future is ready.  They
// lift the return value or error and set result or exception for TaskCompletionSource.
{%- for result_type in ci.iter_async_result_types() %}
{%- let callback_param = result_type.future_callback_param() %}

class {{ result_type|future_callback_handler }} {
    // This cannot be a static method. Although C# supports implicitly using a static method as a
    // delegate, the behaviour is incorrect for this use case. Using static method as a delegate
    // argument creates an implicit delegate object, that is later going to be collected by GC. Any
    // attempt to invoke a garbage collected delegate results in an error:
    //   > A callback was made on a garbage collected delegate of type 'ForeignCallback::..'
    public static UniFfiFutureCallback{{ callback_param|ffi_type_name }} INSTANCE = (IntPtr callbackData, {{ callback_param|ffi_type_name }} returnValue, RustCallStatus callStatus) => {
        var gcHandle = GCHandle.FromIntPtr(callbackData);
        var completionSource = ({{ result_type|future_completion_type }}?)gcHandle.Target;
        try {
            {%- match result_type.throws_type %}
            {%- when Some(throws_type) %}
            _UniffiHelpers.CheckCallStatus({{ throws_type|as_error|ffi_converter_name }}.INSTANCE, callStatus);
            {%- when None %}
            _UniffiHelpers.CheckCallStatus(NullCallStatusErrorHandler.INSTANCE, callStatus);
            {%- endmatch %}

            {%- match result_type.return_type %}
            {%- when Some(return_type) %}
            completionSource!.SetResult({{ return_type|lift_fn }}(returnValue));
            {%- when None %}
            completionSource!.SetResult(true);
            {%- endmatch %}
        } catch (Exception e) {
            completionSource!.SetException(e);
        }
    };
}
{%- endfor %}
