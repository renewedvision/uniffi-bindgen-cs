{#/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */#}

{%- let obj = ci.get_object_definition(name).unwrap() %}
{%- let safe_handle_type = format!("{}SafeHandle", type_name) %}
{%- if self.include_once_check("ObjectRuntime.cs") %}{% include "ObjectRuntime.cs" %}{% endif %}

{%- call cs::docstring(obj, 0) %}
public interface I{{ type_name }} {
    {%- for func in obj.methods() %}
    {%- call cs::docstring(func, 4) %}
    {%- call cs::method_throws_annotation(func.throws_type()) %}
    {% call cs::return_type(func) %} {{ func.name()|fn_name }}({% call cs::arg_list_decl(func) %});
    {%- endfor %}
}

public class {{ safe_handle_type }}: FFISafeHandle {
    public {{ safe_handle_type }}(): base() {
    }
    public {{ safe_handle_type }}(IntPtr pointer): base(pointer) {
    }
    override protected bool ReleaseHandle() {
        _UniffiHelpers.RustCall((ref RustCallStatus status) => {
            _UniFFILib.{{ obj.ffi_object_free().name() }}(this.handle, ref status);
        });
        return true;
    }
}

{%- call cs::docstring(obj, 0) %}
public class {{ type_name }}: FFIObject<{{ safe_handle_type }}>, I{{ type_name }} {
    public {{ type_name }}({{ safe_handle_type }} pointer): base(pointer) {}

    {%- match obj.primary_constructor() %}
    {%- when Some with (cons) %}
    {%- call cs::docstring(cons, 4) %}
    public {{ type_name }}({% call cs::arg_list_decl(cons) -%}) :
        this({% call cs::to_ffi_call(cons) %}) {}
    {%- when None %}
    {%- endmatch %}

    {% for func in obj.methods() -%}
    {%- call cs::docstring(func, 4) %}
    {%- call cs::method_throws_annotation(func.throws_type()) %}
    {%- if func.is_async() %}
    public async {% call cs::return_type(func) %} {{ func.name()|fn_name }}({%- call cs::arg_list_decl(func) -%}) {
        var completionSource = new TaskCompletionSource<{% call cs::task_completion_type(func.return_type()) %}>();
        var gcHandle = GCHandle.Alloc(completionSource);
        try {
            _UniffiHelpers.RustCall((ref RustCallStatus _status) => {
                _UniFFILib.{{ func.ffi_func().name() }}(
                    this.GetHandle(),
                    {%- call cs::lower_arg_list(func) -%}{% if func.arguments().len() > 0 %},{% endif %}
                    IntPtr.Zero,
                    {{ func.result_type().borrow()|future_callback_handler }}.INSTANCE,
                    GCHandle.ToIntPtr(gcHandle),
                    ref _status);
            });
            {%- if func.return_type().is_some() %}
            return await completionSource.Task;
            {%- else %}
            await completionSource.Task;
            {%- endif %}
        } finally {
            gcHandle.Free();
        }
    }
    {%- else %}
    {%- match func.return_type() -%}
    {%- when Some with (return_type) %}
    public {{ return_type|type_name }} {{ func.name()|fn_name }}({% call cs::arg_list_decl(func) %}) {
        return {{ return_type|lift_fn }}({%- call cs::to_ffi_call_with_prefix("this.GetHandle()", func) %});
    }
    {%- when None %}
    public void {{ func.name()|fn_name }}({% call cs::arg_list_decl(func) %}) {
        {%- call cs::to_ffi_call_with_prefix("this.GetHandle()", func) %};
    }
    {%- endmatch %}
    {%- endif %}

    {% endfor %}

    {% if !obj.alternate_constructors().is_empty() -%}
    {% for cons in obj.alternate_constructors() -%}
    {%- call cs::docstring(cons, 4) %}
    {%- call cs::method_throws_annotation(cons.throws_type()) %}
    public static {{ type_name }} {{ cons.name()|fn_name }}({% call cs::arg_list_decl(cons) %}) {
        return new {{ type_name }}({% call cs::to_ffi_call(cons) %});
    }
    {% endfor %}
    {% endif %}
}

class {{ obj|ffi_converter_name }}: FfiConverter<{{ type_name }}, {{ safe_handle_type }}> {
    public static {{ obj|ffi_converter_name }} INSTANCE = new {{ obj|ffi_converter_name }}();

    public override {{ safe_handle_type }} Lower({{ type_name }} value) {
        return value.GetHandle();
    }

    public override {{ type_name }} Lift({{ safe_handle_type }} value) {
        return new {{ type_name }}(value);
    }

    public override {{ type_name }} Read(BigEndianStream stream) {
        return Lift(new {{ safe_handle_type }}(new IntPtr(stream.ReadLong())));
    }

    public override int AllocationSize({{ type_name }} value) {
        return 8;
    }

    public override void Write({{ type_name }} value, BigEndianStream stream) {
        stream.WriteLong(Lower(value).DangerousGetRawFfiValue().ToInt64());
    }
}
